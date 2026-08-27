using System.Globalization;
using System.Speech.Recognition;
using PokerProOS.Application.Voz;

namespace PokerProOS.Voz.Sapi;

public sealed class ReconocedorSapi : IReconocedorDeVoz
{
    private readonly SpeechRecognitionEngine _motor;
    private readonly OpcionesDeVoz _opciones;
    private readonly object _bloqueo = new();

    // Estas tres banderas se leen y escriben tanto desde el hilo llamador
    // (ComenzarEscuchaContinua/Pausar/Reanudar) como desde el hilo de SAPI
    // que dispara RecognizeCompleted. _bloqueo las protege a todas: nunca se
    // llama RecognizeAsync mientras el motor ya está corriendo, ni mientras
    // un cancel pedido no terminó de completarse.
    private bool _escuchaDeseada;
    private bool _pausado;
    private bool _motorEnEjecucion;

    // Se pone en true cuando AlCompletar ve un error real de SAPI y decide,
    // a propósito, dejar el motor detenido (ver el comentario en esa rama).
    // Reanudar() la respeta: sin esto, NoReconocido dispara
    // CopilotoDeVoz.Publicar, que llama Reanudar() en su finally y
    // reengancha el motor que el propio handler de error acaba de decidir
    // detener. Sobre una falla persistente de audio (mic desconectado,
    // cambio de dispositivo) eso es error -> habla -> reintenta -> error,
    // sin fin. ComenzarEscuchaContinua es el único lugar que la limpia: es
    // el pedido explícito de volver a escuchar.
    private bool _detenidoPorError;

    private readonly GeneradorDeGramatica _generador;

    public ReconocedorSapi(GeneradorDeGramatica generador, OpcionesDeVoz opciones)
    {
        _generador = generador;
        _opciones = opciones;
        _motor = new SpeechRecognitionEngine(new CultureInfo(opciones.Cultura));
        _motor.LoadGrammar(generador.Construir());
        _motor.SpeechRecognized += AlReconocer;
        _motor.SpeechRecognitionRejected += AlRechazar;
        // Windows corta la escucha continua tras un rato de silencio.
        // Reengancharla aca es el watchdog; tambien es el unico lugar que ve
        // cuando un cancel pedido por Pausar() realmente terminó.
        _motor.RecognizeCompleted += AlCompletar;
    }

    public event EventHandler<DictadoReconocido>? Reconocido;
    public event EventHandler<string>? NoReconocido;

    public void ComenzarEscuchaContinua()
    {
        lock (_bloqueo)
        {
            _escuchaDeseada = true;
            _pausado = false;
            _detenidoPorError = false;
            if (_motorEnEjecucion) return;
            _motor.SetInputToDefaultAudioDevice();
            _motor.RecognizeAsync(RecognizeMode.Multiple);
            _motorEnEjecucion = true;
        }
    }

    public void Pausar()
    {
        lock (_bloqueo)
        {
            _pausado = true;
            if (_motorEnEjecucion) _motor.RecognizeAsyncCancel();
        }
    }

    public void Reanudar()
    {
        lock (_bloqueo)
        {
            _pausado = false;
            // Un error real ya decidió detener el motor a propósito (ver
            // AlCompletar): no reengancharlo hasta un pedido explícito de
            // ComenzarEscuchaContinua.
            if (_detenidoPorError) return;
            // Si el motor todavia esta corriendo (el cancel de un Pausar
            // previo no completó), no se arranca de nuevo aca: AlCompletar
            // lo hará en cuanto el cancel termine, porque ya ve _pausado
            // en false.
            if (!_escuchaDeseada || _motorEnEjecucion) return;
            _motor.RecognizeAsync(RecognizeMode.Multiple);
            _motorEnEjecucion = true;
        }
    }

    /// <summary>
    /// Usa un motor aparte y descartable en vez de cambiarle la gramática al
    /// vivo: la máquina de estados del reconocedor continuo fue endurecida
    /// para el bucle del copiloto y meterle un modo temporal por el medio es
    /// la forma más fácil de romperla. El motor vivo solo se pausa.
    /// </summary>
    public string? CapturarDictadoLibre(TimeSpan espera)
    {
        Pausar();
        try
        {
            using var temporal = new SpeechRecognitionEngine(new CultureInfo(_opciones.Cultura));
            temporal.LoadGrammar(new DictationGrammar());
            temporal.SetInputToDefaultAudioDevice();
            var resultado = temporal.Recognize(espera);
            temporal.SetInputToNull();
            return string.IsNullOrWhiteSpace(resultado?.Text) ? null : resultado.Text;
        }
        finally
        {
            Reanudar();
        }
    }

    public void RecargarGramatica()
    {
        // Descargar y volver a cargar no interrumpe el reconocimiento en
        // curso: SAPI acepta el cambio de gramaticas con el motor andando.
        _motor.UnloadAllGrammars();
        _motor.LoadGrammar(_generador.Construir());
    }

    public DictadoReconocido? ReconocerArchivo(string rutaWav)
    {
        _motor.SetInputToWaveFile(rutaWav);
        var resultado = _motor.Recognize();
        // Libera el WAV antes de que el llamador intente borrarlo.
        _motor.SetInputToNull();
        return Interpretar(resultado);
    }

    private void AlReconocer(object? remitente, SpeechRecognizedEventArgs argumentos)
    {
        // Este handler corre en el hilo de SAPI, sin nada por encima: la
        // política por default de .NET ante una excepción no atrapada ahí
        // es terminar el proceso. Reconocido/NoReconocido dispara, en el
        // mismo hilo y sincrónicamente, todo el bucle del copiloto
        // (Pausar/Hablar/Reanudar y cualquier suscriptor de Publicado
        // incluidos); una excepción en cualquier punto de esa cadena tiene
        // que morir acá, no tumbar la app en medio de una mano.
        try
        {
            var dictado = Interpretar(argumentos.Result);
            if (dictado is null) NoReconocido?.Invoke(this, argumentos.Result?.Text ?? "");
            else Reconocido?.Invoke(this, dictado);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ReconocedorSapi] Error al procesar un reconocimiento: {ex}");
        }
    }

    private void AlRechazar(object? remitente, SpeechRecognitionRejectedEventArgs argumentos)
    {
        // Mismo hilo de SAPI, mismo riesgo que AlReconocer: NoReconocido
        // dispara la misma cadena sincrónica hacia el copiloto.
        try
        {
            NoReconocido?.Invoke(this, "");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ReconocedorSapi] Error al procesar un rechazo: {ex}");
        }
    }

    private void AlCompletar(object? remitente, RecognizeCompletedEventArgs argumentos)
    {
        // Mismo motivo que en AlReconocer: nada puede escapar de este
        // handler hacia el hilo de SAPI.
        try
        {
            lock (_bloqueo)
            {
                _motorEnEjecucion = false;

                if (argumentos.Error is not null)
                {
                    // No reintentar en bucle sobre un error real: se avisa y
                    // se deja el motor detenido en vez de arriesgar un spin.
                    // _detenidoPorError se pone en true ANTES de avisar
                    // porque NoReconocido puede reengancharse
                    // sincrónicamente hasta Reanudar() (vía
                    // CopilotoDeVoz.Publicar): si Reanudar() no viera la
                    // bandera todavía, reiniciaría el motor que esta misma
                    // rama decidió detener.
                    _detenidoPorError = true;
                    NoReconocido?.Invoke(this, argumentos.Error.Message);
                    return;
                }

                // Se reengancha tanto en el corte normal por silencio (watchdog)
                // como en un cancel que ya completó, siempre que siga habiendo
                // escucha deseada y nadie haya vuelto a pausar mientras tanto.
                if (_escuchaDeseada && !_pausado)
                {
                    _motor.RecognizeAsync(RecognizeMode.Multiple);
                    _motorEnEjecucion = true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ReconocedorSapi] Error al completar un reconocimiento: {ex}");
        }
    }

    private DictadoReconocido? Interpretar(RecognitionResult? resultado)
    {
        if (resultado is null || resultado.Confidence < _opciones.ConfianzaMinima) return null;

        var semantica = resultado.Semantics;
        string? Texto(string clave) =>
            semantica.ContainsKey(clave) ? semantica[clave].Value?.ToString() : null;

        var alta = Texto("alta");
        var baja = Texto("baja");

        decimal? stack = Texto("stack") is { } crudo &&
                         decimal.TryParse(crudo, NumberStyles.Any, CultureInfo.InvariantCulture, out var bb)
            ? bb
            : null;
        var spot = Texto("spot");
        var situacion = Texto("situacion");

        // Sin mano el dictado sigue valiendo: es una orden de contexto
        // ("heads up", "nueve be be", "contra limp"). Lo que no vale es un
        // reconocimiento sin NADA, ni mano ni contexto.
        var hayMano = alta is not null && baja is not null;
        var hayContexto = stack is not null || spot is not null || situacion is not null;
        if (!hayMano && !hayContexto) return null;

        // Los dos rangos van juntos o no van: media mano reconocida se trata
        // como contexto sin mano, no como una consulta a medio armar.
        return new DictadoReconocido(
            stack, spot, situacion,
            hayMano ? alta! : "", hayMano ? baja! : "", hayMano ? Texto("palo") : null,
            resultado.Confidence, resultado.Text);
    }

    public void Dispose()
    {
        lock (_bloqueo)
        {
            _escuchaDeseada = false;
            _pausado = true;
        }
        _motor.Dispose();
    }
}

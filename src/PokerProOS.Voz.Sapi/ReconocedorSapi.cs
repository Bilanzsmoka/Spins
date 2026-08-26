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

    public ReconocedorSapi(GeneradorDeGramatica generador, OpcionesDeVoz opciones)
    {
        _opciones = opciones;
        _motor = new SpeechRecognitionEngine(new CultureInfo(opciones.Cultura));
        _motor.LoadGrammar(generador.Construir());
        _motor.SpeechRecognized += AlReconocer;
        _motor.SpeechRecognitionRejected += (_, _) => NoReconocido?.Invoke(this, "");
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
            // Si el motor todavia esta corriendo (el cancel de un Pausar
            // previo no completó), no se arranca de nuevo aca: AlCompletar
            // lo hará en cuanto el cancel termine, porque ya ve _pausado
            // en false.
            if (!_escuchaDeseada || _motorEnEjecucion) return;
            _motor.RecognizeAsync(RecognizeMode.Multiple);
            _motorEnEjecucion = true;
        }
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
        var dictado = Interpretar(argumentos.Result);
        if (dictado is null) NoReconocido?.Invoke(this, argumentos.Result?.Text ?? "");
        else Reconocido?.Invoke(this, dictado);
    }

    private void AlCompletar(object? remitente, RecognizeCompletedEventArgs argumentos)
    {
        lock (_bloqueo)
        {
            _motorEnEjecucion = false;

            if (argumentos.Error is not null)
            {
                // No reintentar en bucle sobre un error real: se avisa y se
                // deja el motor detenido en vez de arriesgar un spin.
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

    private DictadoReconocido? Interpretar(RecognitionResult? resultado)
    {
        if (resultado is null || resultado.Confidence < _opciones.ConfianzaMinima) return null;

        var semantica = resultado.Semantics;
        string? Texto(string clave) =>
            semantica.ContainsKey(clave) ? semantica[clave].Value?.ToString() : null;

        var alta = Texto("alta");
        var baja = Texto("baja");
        if (alta is null || baja is null) return null;

        decimal? stack = Texto("stack") is { } crudo &&
                         decimal.TryParse(crudo, NumberStyles.Any, CultureInfo.InvariantCulture, out var bb)
            ? bb
            : null;

        return new DictadoReconocido(
            stack, Texto("spot"), Texto("situacion"),
            alta, baja, Texto("palo"),
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

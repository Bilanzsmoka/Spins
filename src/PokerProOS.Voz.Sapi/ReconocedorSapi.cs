using System.Globalization;
using System.Speech.Recognition;
using PokerProOS.Application.Voz;

namespace PokerProOS.Voz.Sapi;

public sealed class ReconocedorSapi : IReconocedorDeVoz
{
    private readonly SpeechRecognitionEngine _motor;
    private readonly OpcionesDeVoz _opciones;
    private bool _escuchaContinua;

    public ReconocedorSapi(GeneradorDeGramatica generador, OpcionesDeVoz opciones)
    {
        _opciones = opciones;
        _motor = new SpeechRecognitionEngine(new CultureInfo(opciones.Cultura));
        _motor.LoadGrammar(generador.Construir());
        _motor.SpeechRecognized += AlReconocer;
        _motor.SpeechRecognitionRejected += (_, _) => NoReconocido?.Invoke(this, "");
        // Windows corta la escucha continua tras un rato de silencio.
        // Reengancharla en RecognizeCompleted es el watchdog.
        _motor.RecognizeCompleted += AlCompletar;
    }

    public event EventHandler<DictadoReconocido>? Reconocido;
    public event EventHandler<string>? NoReconocido;

    public void ComenzarEscuchaContinua()
    {
        _escuchaContinua = true;
        _motor.SetInputToDefaultAudioDevice();
        _motor.RecognizeAsync(RecognizeMode.Multiple);
    }

    public void Pausar()
    {
        if (_escuchaContinua) _motor.RecognizeAsyncCancel();
    }

    public void Reanudar()
    {
        if (_escuchaContinua) _motor.RecognizeAsync(RecognizeMode.Multiple);
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
        if (_escuchaContinua && !argumentos.Cancelled)
            _motor.RecognizeAsync(RecognizeMode.Multiple);
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
        _escuchaContinua = false;
        _motor.Dispose();
    }
}

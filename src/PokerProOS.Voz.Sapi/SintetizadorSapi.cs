using System.Speech.Synthesis;
using PokerProOS.Application.Voz;

namespace PokerProOS.Voz.Sapi;

public sealed class SintetizadorSapi : ISintetizadorDeVoz
{
    private readonly SpeechSynthesizer _sintetizador = new();

    public SintetizadorSapi(OpcionesDeVoz opciones)
    {
        if (!string.IsNullOrWhiteSpace(opciones.Voz))
            _sintetizador.SelectVoice(opciones.Voz);
    }

    public void Hablar(string texto)
    {
        _sintetizador.SetOutputToDefaultAudioDevice();
        _sintetizador.Speak(texto);
    }

    public void HablarAArchivo(string texto, string rutaWav)
    {
        _sintetizador.SetOutputToWaveFile(rutaWav);
        _sintetizador.Speak(texto);
        // Libera el archivo: sin esto un File.Delete posterior falla.
        _sintetizador.SetOutputToNull();
    }

    public void Dispose() => _sintetizador.Dispose();
}

namespace PokerProOS.Application.Voz;

public record OpcionesDeVoz
{
    public string Cultura { get; init; } = "es-ES";
    public string? Voz { get; init; }

    /// <summary>
    /// Umbral por debajo del cual se descarta el reconocimiento. Sobre audio
    /// sintetizado la confianza real medida queda entre 0,48 y 0,64, así que
    /// las pruebas lo bajan. Configurable, nunca fijo en código.
    /// </summary>
    public float ConfianzaMinima { get; init; } = 0.35f;
}

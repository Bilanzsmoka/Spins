namespace PokerProOS.Application.Voz;

/// <summary>El primer elemento de <see cref="Dichos"/> es la forma canónica: la que se usa al hablar de vuelta.</summary>
public record FormasHabladas(string Clave, IReadOnlyList<string> Dichos);

public interface IRegistroDeVocabulario
{
    IReadOnlyList<string> PalabrasDeStack { get; }
    IReadOnlyList<FormasHabladas> Rangos { get; }
    IReadOnlyList<FormasHabladas> Palos { get; }
    IReadOnlyList<FormasHabladas> Spots { get; }
    IReadOnlyList<FormasHabladas> Situaciones { get; }

    /// <summary>
    /// El formato de mesa ("HU", "3-max"). Es lo primero que se dicta al
    /// cambiar de tabla y lo unico que no se podia decir: el selector existia
    /// en pantalla pero la voz no tenia como nombrarlo.
    /// </summary>
    IReadOnlyList<FormasHabladas> Formatos { get; }
}

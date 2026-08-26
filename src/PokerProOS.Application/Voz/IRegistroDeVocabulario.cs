namespace PokerProOS.Application.Voz;

public record FormasHabladas(string Clave, IReadOnlyList<string> Dichos);

public interface IRegistroDeVocabulario
{
    IReadOnlyList<string> PalabrasDeStack { get; }
    IReadOnlyList<FormasHabladas> Rangos { get; }
    IReadOnlyList<FormasHabladas> Palos { get; }
    IReadOnlyList<FormasHabladas> Spots { get; }
    IReadOnlyList<FormasHabladas> Situaciones { get; }
}

namespace PokerProOS.Application.Voz;

/// <summary>
/// Vocabulario reemplazable en caliente. El editor reescribe el JSON y llama a
/// <see cref="Reemplazar"/>; como el intérprete lo relee en cada dictado, una
/// forma hablada nueva funciona sin reiniciar.
/// </summary>
public sealed class VocabularioVivo(IRegistroDeVocabulario inicial) : IRegistroDeVocabulario
{
    private IRegistroDeVocabulario _actual = inicial;

    public void Reemplazar(IRegistroDeVocabulario nuevo) => _actual = nuevo;

    public IReadOnlyList<string> PalabrasDeStack => _actual.PalabrasDeStack;
    public IReadOnlyList<FormasHabladas> Rangos => _actual.Rangos;
    public IReadOnlyList<FormasHabladas> Palos => _actual.Palos;
    public IReadOnlyList<FormasHabladas> Spots => _actual.Spots;
    public IReadOnlyList<FormasHabladas> Situaciones => _actual.Situaciones;
    public IReadOnlyList<FormasHabladas> Formatos => _actual.Formatos;
}

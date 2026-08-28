namespace PokerProOS.Application.Voz;

/// <summary>
/// Las categorías editables del vocabulario. Se usan como texto en la URL, así
/// que el nombre es parte del contrato con la interfaz.
/// </summary>
public enum CategoriaDeVocabulario
{
    Rangos,
    Palos,
    Spots,
    Situaciones,
    PalabrasDeStack,
}

public record ResultadoDeVocabulario(bool Exito, string? Error);

/// <summary>
/// Enseña a la aplicación cómo dice las cosas este usuario. Escribe en
/// vocabulario.json y deja el vocabulario vivo al día, sin reiniciar.
/// </summary>
public interface IEditorDeVocabulario
{
    Task<ResultadoDeVocabulario> AgregarAsync(
        CategoriaDeVocabulario categoria, string clave, string dicho, CancellationToken ct);

    Task<ResultadoDeVocabulario> QuitarAsync(
        CategoriaDeVocabulario categoria, string clave, string dicho, CancellationToken ct);
}

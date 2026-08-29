namespace PokerProOS.Application.Glosario;

/// <summary>Un término del póker y qué significa, en castellano.</summary>
public record TerminoDelGlosario(string Termino, string Explicacion);

/// <summary>Los términos juntados por tema, para que la página se pueda leer.</summary>
public record GrupoDelGlosario(string Clave, string Titulo, IReadOnlyList<TerminoDelGlosario> Terminos);

/// <summary>
/// La jerga del juego, explicada.
///
/// Es material de estudio, no configuración: nada del funcionamiento de la app
/// depende de esto. Por eso, a diferencia de acciones.json y vocabulario.json
/// —sin los cuales no hay nada que servir—, si el archivo falta la app arranca
/// igual y la pantalla del diccionario queda vacía.
/// </summary>
public interface IRegistroDeGlosario
{
    IReadOnlyList<GrupoDelGlosario> Grupos { get; }
}

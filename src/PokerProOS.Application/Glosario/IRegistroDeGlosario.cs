namespace PokerProOS.Application.Glosario;

/// <summary>
/// Un término del póker y qué significa, en castellano.
///
/// Todo lo que va después de la explicación es opcional y existe para los
/// perfiles de jugador, que no se leen: se reconocen. El color y el ícono son
/// dato, no código, porque son los que después van a etiquetar rivales de
/// verdad — y un color que vive en el JSON se corrige sin recompilar nada.
/// </summary>
/// <param name="Eje">Por qué costado clasifica este perfil: qué tan fuerte es, o cómo juega.</param>
/// <param name="Perfil">El perfil en tres palabras: "cerrado y pasivo".</param>
/// <param name="Color">El color del círculo, en hexadecimal.</param>
/// <param name="ColorTexto">Qué color usar encima de <see cref="Color"/>, para que se lea.</param>
/// <param name="Icono">La figura del círculo: un emoji, que no hay que descargar de ningún lado.</param>
/// <param name="Rasgos">Dos o tres señales cortas para reconocerlo en la mesa.</param>
public record TerminoDelGlosario(
    string Termino,
    string Explicacion,
    string? Eje = null,
    string? Perfil = null,
    string? Color = null,
    string? ColorTexto = null,
    string? Icono = null,
    IReadOnlyList<string>? Rasgos = null);

/// <summary>
/// Un costado por el que se clasifica, y qué significan sus colores.
///
/// La nota no es adorno: un color sin convención declarada es una mancha. Acá
/// es donde dice que el verde es plata y el rojo peligro, que es lo que hace
/// que el color sirva para marcar a alguien en la mesa.
/// </summary>
public record EjeDelGlosario(string Clave, string Nota);

/// <summary>
/// Los términos juntados por tema, para que la página se pueda leer.
/// </summary>
/// <param name="Ejes">
/// Los costados por los que se separan sus términos, en el orden en que van en
/// pantalla. Sólo los trae el grupo de jugadores; el resto se lee de corrido.
/// </param>
public record GrupoDelGlosario(
    string Clave,
    string Titulo,
    IReadOnlyList<TerminoDelGlosario> Terminos,
    IReadOnlyList<EjeDelGlosario>? Ejes = null);

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

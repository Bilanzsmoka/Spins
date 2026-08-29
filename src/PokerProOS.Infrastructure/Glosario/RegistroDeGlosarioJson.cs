using System.Text.Json;
using PokerProOS.Application.Glosario;

namespace PokerProOS.Infrastructure.Glosario;

/// <summary>
/// El glosario leído de <c>database/registro/glosario.json</c>.
///
/// A diferencia del registro de acciones y del de vocabulario, éste **no**
/// tumba el arranque si falta o está mal: es material de estudio, y una app
/// que no abre porque le falta un diccionario sería peor que una sin
/// diccionario. Devuelve vacío y la pantalla lo dice.
/// </summary>
public sealed class RegistroDeGlosarioJson : IRegistroDeGlosario
{
    private RegistroDeGlosarioJson(IReadOnlyList<GrupoDelGlosario> grupos) => Grupos = grupos;

    public IReadOnlyList<GrupoDelGlosario> Grupos { get; }

    /// <summary>Vacío si el archivo no está o no se puede leer.</summary>
    public static IRegistroDeGlosario Cargar(string ruta)
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(ruta));

            var grupos = documento.RootElement.GetProperty("grupos").EnumerateArray()
                .Select(g => new GrupoDelGlosario(
                    g.GetProperty("clave").GetString()!,
                    g.GetProperty("titulo").GetString()!,
                    g.GetProperty("terminos").EnumerateArray()
                        .Select(Leer)
                        .ToList()))
                .ToList();

            return new RegistroDeGlosarioJson(grupos);
        }
        catch
        {
            return new RegistroDeGlosarioJson([]);
        }
    }

    /// <summary>
    /// Término y explicación son obligatorios; el resto es la ficha de perfil,
    /// que sólo traen los jugadores. Una palabra suelta del diccionario no
    /// tiene color ni ícono y no por eso está mal escrita.
    /// </summary>
    private static TerminoDelGlosario Leer(JsonElement t) => new(
        t.GetProperty("termino").GetString()!,
        t.GetProperty("explicacion").GetString()!,
        Texto(t, "eje"),
        Texto(t, "perfil"),
        Texto(t, "color"),
        Texto(t, "colorTexto"),
        Texto(t, "icono"),
        Lista(t, "rasgos"));

    private static string? Texto(JsonElement elemento, string propiedad) =>
        elemento.TryGetProperty(propiedad, out var valor) ? valor.GetString() : null;

    private static IReadOnlyList<string>? Lista(JsonElement elemento, string propiedad) =>
        elemento.TryGetProperty(propiedad, out var valor)
            ? valor.EnumerateArray().Select(v => v.GetString()!).ToList()
            : null;
}

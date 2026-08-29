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
                        .Select(t => new TerminoDelGlosario(
                            t.GetProperty("termino").GetString()!,
                            t.GetProperty("explicacion").GetString()!))
                        .ToList()))
                .ToList();

            return new RegistroDeGlosarioJson(grupos);
        }
        catch
        {
            return new RegistroDeGlosarioJson([]);
        }
    }
}

using System.Text.Json;
using PokerProOS.Application.Plan;

namespace PokerProOS.Infrastructure.Plan;

/// <summary>
/// El plan leído de <c>database/registro/plan.json</c>.
///
/// Como el glosario y a diferencia de acciones.json: si falta o está mal, la
/// app arranca igual y la pantalla no muestra panel. Un plan de estudio es
/// material del usuario; que no se pueda leer no es razón para no servir una
/// tabla.
/// </summary>
public sealed class RegistroDelPlanJson : IRegistroDelPlan
{
    private RegistroDelPlanJson(PlanDefinido plan) => Plan = plan;

    public PlanDefinido Plan { get; }

    /// <summary>Vacío si el archivo no está o no se puede leer.</summary>
    public static IRegistroDelPlan Cargar(string ruta)
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(ruta));
            var raiz = documento.RootElement;

            var hitos = raiz.TryGetProperty("hitos", out var lista)
                ? lista.EnumerateArray().Select(Leer).ToList()
                : [];

            return new RegistroDelPlanJson(new PlanDefinido(
                Numero(raiz, "metaDeVolumen"),
                Texto(raiz, "habitoDeVolumen") ?? "",
                Texto(raiz, "habitoDeEstudio") ?? "",
                hitos));
        }
        catch
        {
            return new RegistroDelPlanJson(PlanDefinido.Vacio);
        }
    }

    /// <summary>
    /// Clave, título, tipo y objetivo los tiene todo hito; el resto depende de
    /// su tipo y se lee si está. Un hito al que le falte lo suyo no se
    /// descarta acá: lo reporta el medidor, con la causa en pantalla.
    /// </summary>
    private static HitoDefinido Leer(JsonElement h) => new(
        Texto(h, "clave") ?? "",
        Texto(h, "titulo") ?? "",
        Texto(h, "tipo") ?? "",
        Numero(h, "objetivo"),
        Texto(h, "situacion"),
        Numero(h, "escalonMinimo"),
        Texto(h, "habito"),
        Numero(h, "dias"));

    private static string? Texto(JsonElement elemento, string propiedad) =>
        elemento.TryGetProperty(propiedad, out var valor) ? valor.GetString() : null;

    private static int Numero(JsonElement elemento, string propiedad) =>
        elemento.TryGetProperty(propiedad, out var valor) && valor.TryGetInt32(out var numero)
            ? numero
            : 0;
}

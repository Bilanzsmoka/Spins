using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Diario;
using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Plan;
using PokerProOS.Application.Tablas;

namespace PokerProOS.Api.Controllers;

/// <summary>
/// El plan de estudio del día: cuánto llevás de volumen, si estudiaste, y qué
/// hito está activo.
///
/// Es el único lugar que junta las cuatro fuentes —el plan del JSON, el
/// catálogo en memoria, el progreso del entrenador y las marcas de hábitos—;
/// el medidor las recibe ya cargadas y no sabe de dónde salieron.
/// </summary>
[ApiController]
[Route("api/plan")]
public sealed class PlanController(
    IRegistroDelPlan plan,
    ICatalogoDeTablas catalogo,
    IRegistroDeHabitos habitos,
    IProgresoDeEntrenamiento progreso,
    IRepositorioDeDiario diario,
    ILogger<PlanController> registro) : ControllerBase
{
    /// <summary>
    /// Cuántos días de marcas se traen. Alcanza para la tira de la semana y
    /// para la ventana de cualquier hito de jugar sin pedir el historial
    /// entero en cada carga de pantalla.
    /// </summary>
    private const int DiasQueSeMiran = 60;

    /// <summary>Quién entrena. Mismo criterio que el entrenador: un solo lugar.</summary>
    private static int UsuarioActual => 1;

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    [HttpGet("hoy")]
    public async Task<IActionResult> Hoy_(CancellationToken ct)
    {
        // Sin plan no hay nada que medir, y no es un error: es que todavía no
        // escribiste uno. La pantalla no dibuja el panel y sigue como estaba.
        if (!plan.Plan.HayPlan) return Ok(new { hayPlan = false });

        var hoy = Hoy;

        try
        {
            var casillas = await progreso.TodasAsync(UsuarioActual, ct);
            var grilla = await diario.ProgresoAsync(hoy.AddDays(-DiasQueSeMiran + 1), hoy, ct);

            var estado = MedidorDeHitos.Medir(
                plan.Plan, catalogo, habitos, casillas, grilla.Dias, hoy);

            return Ok(new { hayPlan = true, estado });
        }
        catch (Exception ex) when (EsFalloDeBase(ex))
        {
            // El plan mide lo que hiciste, y eso vive en la base: sin base no
            // hay plan que mostrar. Se dice, no se disfraza de "vas 0%" — que
            // sería mentirle a alguien sobre su propio progreso.
            registro.LogWarning(ex, "No se pudo leer el progreso para armar el plan del día.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "No puedo leer tu progreso ahora, así que no sé cómo venís. Las tablas y la voz siguen andando." });
        }
    }

    private static bool EsFalloDeBase(Exception excepcion)
    {
        for (Exception? actual = excepcion; actual is not null; actual = actual.InnerException)
            if (actual is DbException or DbUpdateException) return true;
        return false;
    }
}

using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Tablas;

namespace PokerProOS.Api.Controllers;

/// <summary>Lo que la pantalla pide para arrancar una tanda.</summary>
public record TandaPedida(
    string? Formato, string? Situacion, decimal? MinBB, decimal? MaxBB, string? Spot,
    int Tamano = 20);

[ApiController]
[Route("api/entrenador")]
public sealed class EntrenadorController(
    ArmarTandaHandler armar,
    ResponderRespuestaHandler responder,
    ICatalogoDeTablas catalogo,
    IRegistroDeAcciones acciones) : ControllerBase
{
    /// <summary>
    /// El techo de una tanda. Sin esto, un cuerpo con `tamano: 5000000` haría
    /// que el planificador recorra las 57.000 casillas y arme una respuesta
    /// enorme, sin que nadie lo haya pedido de verdad.
    /// </summary>
    public const int TamanoMaximo = 100;

    /// <summary>
    /// Quién está entrenando. El spec pide que el usuario sea parte de la
    /// clave del progreso desde el primer día pero no construye login: este es
    /// EL único lugar donde se decide, para que agregar identidad sea cambiar
    /// de dónde sale este número y nada más.
    /// </summary>
    private static int UsuarioActual => 1;

    /// <summary>
    /// El hoy del calendario. Los handlers no tienen reloj propio —así se
    /// prueban sin depender del día en que corren—, y quien lo tiene es el
    /// borde, que es acá.
    /// </summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    [HttpPost("tanda")]
    public async Task<IActionResult> Tanda([FromBody] TandaPedida pedida, CancellationToken ct)
    {
        var tamano = Math.Clamp(pedida.Tamano, 1, TamanoMaximo);
        var filtro = new FiltroDeTanda(
            pedida.Formato, pedida.Situacion, pedida.MinBB, pedida.MaxBB, pedida.Spot);

        var preguntas = await armar.ArmarAsync(UsuarioActual, filtro, tamano, Hoy, ct);
        return Ok(preguntas);
    }

    [HttpPost("respuesta")]
    public async Task<IActionResult> Responder(
        [FromBody] RespuestaEnviada respuesta, CancellationToken ct)
    {
        var veredicto = await responder.ResponderAsync(UsuarioActual, respuesta, Hoy, ct);

        // La tabla pudo haberse corregido entre que se armo la tanda y se
        // contesto. No es un error del usuario: la pantalla saltea la pregunta.
        return veredicto is null
            ? NotFound(new { error = "Esa casilla ya no existe en el catálogo." })
            : Ok(veredicto);
    }

    /// <summary>
    /// Las acciones que ese spot usa de verdad, con su color y su orden del
    /// registro. Salen del spot y no de una lista en código: si la grilla
    /// pinta ALL-IN de verde, el botón es verde, y romper esa memoria visual
    /// sería entrenar dos cosas distintas.
    /// </summary>
    [HttpGet("acciones")]
    public IActionResult Acciones(
        [FromQuery] string situacion, [FromQuery] string stack, [FromQuery] string spot)
    {
        var tabla = catalogo.Spot(situacion, stack, spot);
        if (tabla is null)
            return NotFound(new { error = "Ese spot no existe." });

        var delSpot = tabla.Conteos.Keys
            .Where(acciones.Existe)
            .Select(acciones.Obtener)
            .OrderBy(a => a.Orden)
            .ToList();

        return Ok((IReadOnlyList<AccionDefinida>)delSpot);
    }
}

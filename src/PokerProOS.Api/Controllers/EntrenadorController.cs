using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Tablas;

namespace PokerProOS.Api.Controllers;

/// <summary>Lo que la pantalla pide para arrancar una tanda.</summary>
/// <param name="Tamano">
/// Diez y no veinte. Las ráfagas cortas y frecuentes rinden más que las tandas
/// largas y espaciadas, y además se hacen: la tanda que no arrancás porque da
/// pereza no enseña nada.
/// </param>
public record TandaPedida(
    string? Formato, string? Situacion, decimal? MinBB, decimal? MaxBB, string? Spot,
    int Tamano = 10);

/// <summary>Lo que se dijo, sin interpretar, más qué casilla se estaba contestando.</summary>
public record RespuestaHablada(
    string Situacion, string ClaveDeStack, string Spot, string Mano, string? Texto,
    int Milisegundos = 0);

[ApiController]
[Route("api/entrenador")]
public sealed class EntrenadorController(
    ArmarTandaHandler armar,
    ResponderRespuestaHandler responder,
    ICatalogoDeTablas catalogo,
    IRegistroDeAcciones acciones,
    InterpretadorDeRespuesta interprete,
    IBitacoraDeRespuestas bitacora,
    ILogger<EntrenadorController> registro) : ControllerBase
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

        try
        {
            var preguntas = await armar.ArmarAsync(UsuarioActual, filtro, tamano, Hoy, ct);
            return Ok(preguntas);
        }
        catch (Exception ex) when (EsFalloDeBase(ex))
        {
            return BaseCaida(ex, "no se pudo armar la tanda");
        }
    }

    /// <summary>
    /// Lo que más veces erraste igual. Es el material más valioso que tiene el
    /// entrenador: no lo que no sabés, sino lo que sabés mal.
    /// </summary>
    [HttpGet("errores")]
    public async Task<IActionResult> Errores(
        [FromQuery] int cuantos = 10, CancellationToken ct = default)
    {
        try
        {
            return Ok(await bitacora.ErroresRepetidosAsync(
                UsuarioActual, Math.Clamp(cuantos, 1, 50), ct));
        }
        catch (Exception ex) when (EsFalloDeBase(ex))
        {
            return BaseCaida(ex, "no se pudieron leer tus errores");
        }
    }

    /// <summary>
    /// Cuánto llevás jugado, cuánto acertás y qué spots te salen peor.
    /// </summary>
    [HttpGet("rendimiento")]
    public async Task<IActionResult> Rendimiento(
        [FromQuery] int spots = 12, [FromQuery] int minimo = 5, CancellationToken ct = default)
    {
        try
        {
            return Ok(await bitacora.RendimientoAsync(
                UsuarioActual, Math.Clamp(spots, 1, 60), Math.Clamp(minimo, 1, 100), ct));
        }
        catch (Exception ex) when (EsFalloDeBase(ex))
        {
            return BaseCaida(ex, "no se pudo leer tu rendimiento");
        }
    }

    [HttpPost("respuesta")]
    public async Task<IActionResult> Responder(
        [FromBody] RespuestaEnviada respuesta, CancellationToken ct)
    {
        VeredictoDeRespuesta? veredicto;
        try
        {
            veredicto = await responder.ResponderAsync(UsuarioActual, respuesta, Hoy, ct);
        }
        catch (Exception ex) when (EsFalloDeBase(ex))
        {
            return BaseCaida(ex, "tu respuesta no quedó guardada");
        }

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

    /// <summary>
    /// Contestar hablando. El texto que no es una acción se ignora con 200 y
    /// no cuenta como fallo: hablar cerca del micrófono no puede ensuciarte el
    /// calendario, y un 400 pintaría la consola de rojo por conversar.
    /// </summary>
    [HttpPost("respuesta-hablada")]
    public async Task<IActionResult> ResponderHablado(
        [FromBody] RespuestaHablada hablada, CancellationToken ct)
    {
        var accion = interprete.Interpretar(hablada.Texto ?? "");
        if (accion is null) return Ok(new { ignorado = true });

        VeredictoDeRespuesta? veredicto;
        try
        {
            veredicto = await responder.ResponderAsync(
                UsuarioActual,
                new RespuestaEnviada(
                    hablada.Situacion, hablada.ClaveDeStack, hablada.Spot, hablada.Mano, accion,
                    hablada.Milisegundos),
                Hoy, ct);
        }
        catch (Exception ex) when (EsFalloDeBase(ex))
        {
            return BaseCaida(ex, "tu respuesta no quedó guardada");
        }

        return veredicto is null
            ? NotFound(new { error = "Esa casilla ya no existe en el catálogo." })
            : Ok(veredicto);
    }

    /// <summary>
    /// El entrenador es lo único de la app que NO anda sin base de datos, y
    /// <c>ProgresoDeEntrenamientoSql</c> no se traga la excepción a propósito:
    /// un calendario que pierde respuestas en silencio no es un calendario.
    /// Quien la traduce a algo legible es este borde. Sin esto la excepción
    /// llegaba al middleware genérico y el usuario leía
    /// «An internal error occurred», en inglés y sin enterarse de que lo que
    /// falta es la base.
    ///
    /// 503 y no 500: no está roto, está caído — reintentar con SQL Server
    /// arriba es exactamente lo que corresponde hacer.
    /// </summary>
    private ObjectResult BaseCaida(Exception ex, string consecuencia)
    {
        // El texto real del fallo no viaja a la pantalla —no le dice nada a
        // quien está estudiando— pero tiene que quedar en algún lado o
        // diagnosticar por qué no conecta se vuelve adivinanza.
        registro.LogError(ex, "El entrenador no pudo hablar con la base de datos");

        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new
            {
                error = "El entrenador necesita la base de datos para llevar tu calendario "
                        + $"de repaso, y no pudo conectarse: {consecuencia}. "
                        + "El resto de la app funciona sin ella."
            });
    }

    /// <summary>
    /// Si el fallo viene de la base. Se recorre la cadena porque EF envuelve:
    /// un SQL Server apagado al guardar llega como DbUpdateException con la
    /// excepción del proveedor adentro. Se mira DbException —de
    /// System.Data.Common— y no el tipo de un proveedor concreto: la Api no
    /// tiene por qué saber que abajo hay SQL Server.
    /// </summary>
    private static bool EsFalloDeBase(Exception excepcion)
    {
        for (Exception? actual = excepcion; actual is not null; actual = actual.InnerException)
            if (actual is DbException or DbUpdateException) return true;
        return false;
    }
}

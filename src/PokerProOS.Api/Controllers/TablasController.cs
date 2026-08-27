using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Tablas;

namespace PokerProOS.Api.Controllers;

public record ParteEnviada(string Accion, int Frecuencia);

public record CeldaEnviada(string? Accion, List<ParteEnviada>? Mix);

public record TipEnviado(string? Texto);

[ApiController]
[Route("api/tablas")]
public sealed class TablasController(
    ICatalogoDeTablas catalogo,
    IRegistroDeAcciones acciones,
    IEditorDeTablas editor,
    AnalizadorDeMemoria analizador) : ControllerBase
{
    /// <summary>
    /// Cambia lo que la tabla prescribe para una mano. Escribe en el JSON,
    /// que es la fuente de verdad, y recarga el catálogo en caliente.
    /// </summary>
    [HttpPut("{situacion}/{stack}/{spot}/{mano}")]
    public async Task<IActionResult> EditarCelda(
        string situacion, string stack, string spot, string mano,
        [FromBody] CeldaEnviada enviada, CancellationToken ct)
    {
        var resultado = await editor.EditarAsync(new EdicionDeCelda(
            situacion, stack, spot, mano,
            enviada.Accion,
            enviada.Mix?.Select(p => new ParteDeMix(p.Accion, p.Frecuencia)).ToList()), ct);

        return resultado.Exito
            ? Ok(new { problemas = resultado.Problemas })
            : BadRequest(new { error = resultado.Error });
    }

    [HttpGet]
    public IActionResult Catalogo() => Ok(new
    {
        acciones = acciones.Todas,
        situaciones = catalogo.Situaciones.Select(s => new
        {
            s.Clave,
            s.Etiqueta,
            s.Formato,
            stacks = s.Stacks.Select(t => new
            {
                t.Stack.Clave,
                t.Stack.MinBB,
                t.Stack.MaxBB,
                spots = t.Spots.Select(p => new { p.Clave, p.Etiqueta })
            })
        }),
        problemas = catalogo.Problemas
    });

    [HttpGet("{situacion}/{stack}/{spot}")]
    public IActionResult Spot(string situacion, string stack, string spot)
    {
        var encontrado = catalogo.Spot(situacion, stack, spot);
        return encontrado is null
            ? NotFound(new { error = $"No existe el spot {spot} en {stack}." })
            : Ok(new { encontrado.Clave, encontrado.Etiqueta, encontrado.Celdas, encontrado.Conteos });
    }

    /// <summary>
    /// Todo lo que hay que saber de una mano en un spot. Existe aparte del
    /// evento de voz para poder estudiar tocando la grilla, sin micrófono.
    /// </summary>
    [HttpGet("ficha")]
    public IActionResult Ficha(
        [FromQuery] string situacion, [FromQuery] string stack,
        [FromQuery] string spot, [FromQuery] string mano)
    {
        var ficha = analizador.Analizar(situacion, stack, spot, mano);
        return ficha is null
            ? NotFound(new { error = $"No tengo ficha de {mano} en {stack}/{spot}." })
            : Ok(ficha);
    }

    /// <summary>
    /// El porqué escrito a mano. Como la edición de celda, escribe el JSON —la
    /// fuente de verdad— y recarga el catálogo en caliente.
    /// </summary>
    [HttpPut("{situacion}/{stack}/{spot}/tip")]
    public async Task<IActionResult> EditarTip(
        string situacion, string stack, string spot,
        [FromBody] TipEnviado enviado, CancellationToken ct)
    {
        var resultado = await editor.EditarTipAsync(
            new EdicionDeTip(situacion, stack, spot, enviado.Texto), ct);

        return resultado.Exito
            ? Ok(new { problemas = resultado.Problemas })
            : BadRequest(new { error = resultado.Error });
    }
}

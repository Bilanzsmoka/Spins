using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Tablas;

namespace PokerProOS.Api.Controllers;

public record ParteEnviada(string Accion, int Frecuencia);

public record CeldaEnviada(string? Accion, List<ParteEnviada>? Mix);

[ApiController]
[Route("api/tablas")]
public sealed class TablasController(
    ICatalogoDeTablas catalogo,
    IRegistroDeAcciones acciones,
    IEditorDeTablas editor) : ControllerBase
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
}

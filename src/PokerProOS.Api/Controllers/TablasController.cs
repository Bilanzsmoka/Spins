using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Tablas;

namespace PokerProOS.Api.Controllers;

[ApiController]
[Route("api/tablas")]
public sealed class TablasController(
    ICatalogoDeTablas catalogo,
    IRegistroDeAcciones acciones) : ControllerBase
{
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

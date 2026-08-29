using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Glosario;

namespace PokerProOS.Api.Controllers;

/// <summary>
/// La jerga del juego. Es material de estudio y sale de un JSON como todo lo
/// demás: agregar un término es editar el archivo, no tocar código.
/// </summary>
[ApiController]
[Route("api/glosario")]
public sealed class GlosarioController(IRegistroDeGlosario glosario) : ControllerBase
{
    [HttpGet]
    public IActionResult Todo() => Ok(new { glosario.Grupos });
}

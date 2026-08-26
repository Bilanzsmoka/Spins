using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Diario;
using PokerProOS.Domain.Diario;

namespace PokerProOS.Api.Controllers;

public record EntradaEnviada(
    string? Intencion,
    string? NivelDeJuego,
    string? Disparador,
    int? Mesas,
    int? Minutos,
    string? Notas);

[ApiController]
[Route("api/diario")]
public sealed class DiarioController(IRepositorioDeDiario repositorio) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int limite = 60, CancellationToken ct = default)
        => Ok(await repositorio.ListarAsync(Math.Clamp(limite, 1, 365), ct));

    /// <summary>La entrada del día más su resumen automático de consultas.</summary>
    [HttpGet("{fecha}")]
    public async Task<IActionResult> Obtener(DateOnly fecha, CancellationToken ct)
        => Ok(new
        {
            entrada = await repositorio.ObtenerAsync(fecha, ct),
            resumen = await repositorio.ResumirAsync(fecha, ct)
        });

    [HttpPut("{fecha}")]
    public async Task<IActionResult> Guardar(DateOnly fecha, [FromBody] EntradaEnviada enviada, CancellationToken ct)
    {
        var nivel = enviada.NivelDeJuego?.Trim().ToUpperInvariant();
        if (nivel is not null && nivel is not ("A" or "B" or "C" or ""))
            return BadRequest(new { error = "El nivel de juego solo puede ser A, B o C." });

        var guardada = await repositorio.GuardarAsync(new EntradaDeDiario
        {
            Fecha = fecha,
            Intencion = Vacio(enviada.Intencion),
            NivelDeJuego = Vacio(nivel),
            Disparador = Vacio(enviada.Disparador),
            Mesas = enviada.Mesas,
            Minutos = enviada.Minutos,
            Notas = enviada.Notas?.Trim() ?? string.Empty
        }, ct);

        return Ok(guardada);
    }

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

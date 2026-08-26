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
    string? Notas,
    string? ObjetivoTecnico,
    int? CumplimientoObjetivo,
    Dictionary<string, int>? Habitos);

[ApiController]
[Route("api/diario")]
public sealed class DiarioController(
    IRepositorioDeDiario repositorio,
    IRegistroDeHabitos habitos) : ControllerBase
{
    /// <summary>El cuadro de hábitos, con su ayuda. Sale del registro en datos.</summary>
    [HttpGet("habitos")]
    public IActionResult Habitos() => Ok(habitos.Todos);

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int limite = 60, CancellationToken ct = default)
        => Ok(await repositorio.ListarAsync(Math.Clamp(limite, 1, 365), ct));

    /// <summary>La entrada del día más su resumen automático de consultas.</summary>
    [HttpGet("{fecha}")]
    public async Task<IActionResult> Obtener(DateOnly fecha, CancellationToken ct)
        => Ok(new
        {
            entrada = await repositorio.ObtenerAsync(fecha, ct),
            resumen = await repositorio.ResumirAsync(fecha, ct),
            marcas = await repositorio.MarcasAsync(fecha, ct),
            comparativa = await repositorio.CompararAsync(fecha, ct)
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
            Notas = enviada.Notas?.Trim() ?? string.Empty,
            ObjetivoTecnico = Vacio(enviada.ObjetivoTecnico),
            CumplimientoObjetivo = enviada.CumplimientoObjetivo
        }, ct);

        // Las claves que no esten en el registro se descartan en silencio:
        // un habito borrado del JSON no debe romper el guardado de dias viejos.
        var marcas = (enviada.Habitos ?? [])
            .Where(par => habitos.Existe(par.Key))
            .ToDictionary(par => par.Key, par => par.Value);
        await repositorio.GuardarMarcasAsync(fecha, marcas, ct);

        return Ok(guardada);
    }

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

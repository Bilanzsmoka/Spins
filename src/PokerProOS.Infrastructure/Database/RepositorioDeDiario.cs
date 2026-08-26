using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Diario;
using PokerProOS.Domain.Diario;

namespace PokerProOS.Infrastructure.Database;

public sealed class RepositorioDeDiario(PokerProOSDbContext contexto) : IRepositorioDeDiario
{
    public Task<EntradaDeDiario?> ObtenerAsync(DateOnly fecha, CancellationToken ct) =>
        contexto.EntradasDeDiario.FirstOrDefaultAsync(e => e.Fecha == fecha, ct);

    public async Task<IReadOnlyList<EntradaDeDiario>> ListarAsync(int limite, CancellationToken ct) =>
        await contexto.EntradasDeDiario
            .OrderByDescending(e => e.Fecha)
            .Take(limite)
            .ToListAsync(ct);

    public async Task<EntradaDeDiario> GuardarAsync(EntradaDeDiario entrada, CancellationToken ct)
    {
        var existente = await ObtenerAsync(entrada.Fecha, ct);
        if (existente is null)
        {
            entrada.CreadaEn = DateTime.UtcNow;
            entrada.ActualizadaEn = entrada.CreadaEn;
            contexto.EntradasDeDiario.Add(entrada);
            await contexto.SaveChangesAsync(ct);
            return entrada;
        }

        existente.Intencion = entrada.Intencion;
        existente.NivelDeJuego = entrada.NivelDeJuego;
        existente.Disparador = entrada.Disparador;
        existente.Mesas = entrada.Mesas;
        existente.Minutos = entrada.Minutos;
        existente.Notas = entrada.Notas;
        existente.ActualizadaEn = DateTime.UtcNow;
        await contexto.SaveChangesAsync(ct);
        return existente;
    }

    /// <summary>
    /// Lo que ningún tracker tiene: qué manos consultó ese día. Un tracker ve
    /// las manos que jugó; esto ve las que no sabía.
    /// </summary>
    public async Task<ResumenDelDia> ResumirAsync(DateOnly fecha, CancellationToken ct)
    {
        var desde = fecha.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var hasta = desde.AddDays(1);

        var consultas = await contexto.ConsultasDeVoz
            .Where(c => c.CreadaEn >= desde && c.CreadaEn < hasta)
            .ToListAsync(ct);

        if (consultas.Count == 0)
            return new ResumenDelDia(0, 0, [], null, null);

        var top = consultas
            .Where(c => c.Resuelta && c.Mano.Length > 0)
            .GroupBy(c => new { c.Mano, c.Accion })
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.Mano)
            .Take(8)
            .Select(g => new ManoConsultada(g.Key.Mano, g.Key.Accion, g.Count()))
            .ToList();

        static string Hora(DateTime utc) => utc.ToLocalTime().ToString("HH:mm");

        return new ResumenDelDia(
            consultas.Count,
            consultas.Count(c => c.Resuelta),
            top,
            Hora(consultas.Min(c => c.CreadaEn)),
            Hora(consultas.Max(c => c.CreadaEn)));
    }
}

using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Entrenador;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Infrastructure.Entrenador;

/// <summary>
/// La bitácora de respuestas en SQL Server.
///
/// No se traga los errores, por lo mismo que el progreso: el entrenador es lo
/// único de la app que no anda sin base, y una respuesta que se pierde en
/// silencio es peor que un cartel.
/// </summary>
public sealed class BitacoraDeRespuestasSql(PokerProOSDbContext contexto) : IBitacoraDeRespuestas
{
    public async Task RegistrarAsync(RespuestaRegistrada respuesta, CancellationToken ct)
    {
        contexto.RespuestasRegistradas.Add(respuesta);
        await contexto.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ErrorRepetido>> ErroresRepetidosAsync(
        int usuarioId, int cuantos, CancellationToken ct)
    {
        // La agrupación se proyecta a un tipo anónimo y el record se arma
        // después, en memoria: EF no sabe traducir la construcción de un record
        // dentro de un GroupBy y tira en tiempo de ejecución, no al compilar.
        var agrupadas = await contexto.RespuestasRegistradas
            .Where(r => r.UsuarioId == usuarioId && !r.Acerto)
            .GroupBy(r => new
            {
                r.Situacion, r.ClaveDeStack, r.Spot, r.Mano, r.AccionElegida, r.AccionCorrecta,
            })
            // Una equivocación suelta es ruido; lo que se repite es un hueco.
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                g.Key,
                Veces = g.Count(),
                Ultima = g.Max(r => r.RespondidaEn),
            })
            .OrderByDescending(g => g.Veces)
            .ThenByDescending(g => g.Ultima)
            .Take(cuantos)
            .ToListAsync(ct);

        return agrupadas
            .Select(g => new ErrorRepetido(
                g.Key.Situacion, g.Key.ClaveDeStack, g.Key.Spot, g.Key.Mano,
                g.Key.AccionElegida, g.Key.AccionCorrecta,
                g.Veces, g.Ultima))
            .ToList();
    }
}

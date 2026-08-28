using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Entrenador;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Infrastructure.Entrenador;

/// <summary>
/// El progreso contra EF. Sin try/catch: a diferencia de la bitácora, acá una
/// base caída NO se traga en silencio — un calendario de repetición que pierde
/// respuestas no es un calendario, y el spec pide que el entrenador lo diga en
/// pantalla en vez de fallar callado. Quien traduce la excepción a un mensaje
/// es el controlador.
/// </summary>
public sealed class ProgresoDeEntrenamientoSql(PokerProOSDbContext contexto)
    : IProgresoDeEntrenamiento
{
    public async Task<IReadOnlyList<ProgresoDeCasilla>> VencidasAsync(
        int usuarioId, DateOnly hoy, CancellationToken ct)
        => await contexto.ProgresosDeCasilla
            .Where(p => p.UsuarioId == usuarioId && p.Vence <= hoy)
            .OrderBy(p => p.Vence).ThenBy(p => p.Mano)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProgresoDeCasilla>> TodasAsync(
        int usuarioId, CancellationToken ct)
        => await contexto.ProgresosDeCasilla
            .Where(p => p.UsuarioId == usuarioId)
            .ToListAsync(ct);

    public Task<ProgresoDeCasilla?> BuscarAsync(
        int usuarioId, string situacion, string claveDeStack, string spot, string mano,
        CancellationToken ct)
        => contexto.ProgresosDeCasilla.FirstOrDefaultAsync(
            p => p.UsuarioId == usuarioId
                 && p.Situacion == situacion
                 && p.ClaveDeStack == claveDeStack
                 && p.Spot == spot
                 && p.Mano == mano,
            ct);

    public async Task GuardarAsync(ProgresoDeCasilla progreso, CancellationToken ct)
    {
        progreso.ActualizadaEn = DateTime.UtcNow;
        // Id 0 es una fila que nunca se guardó. Una que vino de BuscarAsync ya
        // la está siguiendo el contexto, así que alcanza con SaveChanges.
        if (progreso.Id == 0) contexto.ProgresosDeCasilla.Add(progreso);
        await contexto.SaveChangesAsync(ct);
    }
}

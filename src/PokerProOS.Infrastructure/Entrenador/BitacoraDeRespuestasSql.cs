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
}

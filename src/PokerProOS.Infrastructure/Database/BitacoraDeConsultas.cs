using Microsoft.Extensions.Logging;
using PokerProOS.Application.Bitacora;
using PokerProOS.Application.Voz;
using PokerProOS.Domain.Bitacora;

namespace PokerProOS.Infrastructure.Database;

public sealed class BitacoraDeConsultas(
    PokerProOSDbContext contexto,
    ILogger<BitacoraDeConsultas> registro) : IBitacoraDeConsultas
{
    public async Task RegistrarAsync(EventoDeCopiloto evento, CancellationToken cancelacion)
    {
        try
        {
            contexto.ConsultasDeVoz.Add(new ConsultaDeVoz
            {
                Situacion = evento.Situacion ?? "",
                ClaveDeStack = evento.ClaveDeStack ?? "",
                Spot = evento.Spot ?? "",
                Mano = evento.ManoInterpretada,
                Accion = evento.Accion,
                Respuesta = evento.Respuesta,
                Resuelta = evento.Resuelta,
                TextoCrudo = evento.TextoCrudo,
                CreadaEn = DateTime.UtcNow
            });
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (Exception ex)
        {
            // La herramienta de estudio no se cae porque la base no este.
            registro.LogWarning(ex, "No se pudo registrar la consulta en la bitácora.");
        }
    }
}

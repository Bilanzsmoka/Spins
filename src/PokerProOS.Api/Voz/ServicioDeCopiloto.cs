using PokerProOS.Application.Bitacora;
using PokerProOS.Application.Voz;

namespace PokerProOS.Api.Voz;

/// <summary>
/// Enciende el copiloto al arrancar la aplicación. Si el motor de voz no
/// está disponible, la aplicación sigue funcionando sin voz: las tablas
/// se consultan igual desde la pantalla.
/// </summary>
public sealed class ServicioDeCopiloto(
    CopilotoDeVoz copiloto,
    IReconocedorDeVoz reconocedor,
    CanalDeEventos canal,
    IServiceScopeFactory fabricaDeAlcances,
    ILogger<ServicioDeCopiloto> registro) : BackgroundService
{
    public bool Escuchando { get; private set; }
    public string? Falla { get; private set; }

    /// <summary>
    /// Mensaje de la última falla de síntesis (<see cref="CopilotoDeVoz.FalloAlHablar"/>).
    /// Distinto de <see cref="Falla"/>: esta es una falla puntual al hablar
    /// una respuesta, no una falla de arranque del motor. El reconocedor
    /// sigue vivo; solo esa respuesta se quedó muda.
    /// </summary>
    public string? FallaAlHablar { get; private set; }

    protected override Task ExecuteAsync(CancellationToken cancelacion)
    {
        try
        {
            copiloto.Conectar();
            copiloto.Publicado += (_, evento) =>
            {
                canal.Publicar(evento);
                _ = RegistrarEnBitacoraAsync(evento, cancelacion);
            };
            copiloto.FalloAlHablar += (_, ex) =>
            {
                FallaAlHablar = ex.Message;
                registro.LogError(ex, "Falló la síntesis de voz al hablar una respuesta.");
            };
            reconocedor.ComenzarEscuchaContinua();
            Escuchando = true;
            registro.LogInformation("Copiloto de voz escuchando.");
        }
        catch (Exception ex)
        {
            Falla = ex.Message;
            Escuchando = false;
            registro.LogError(ex, "No se pudo iniciar el copiloto de voz. La aplicación sigue sin voz.");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// El contexto de base de datos es Scoped y este servicio es Singleton:
    /// no se puede inyectar la bitácora directamente. Se crea un alcance por
    /// evento para resolverla y se descarta al terminar. Igual que
    /// <see cref="BitacoraDeConsultas.RegistrarAsync"/>, no puede propagar:
    /// se llama en fuego y olvido desde el callback de reconocimiento.
    /// </summary>
    private async Task RegistrarEnBitacoraAsync(EventoDeCopiloto evento, CancellationToken cancelacion)
    {
        try
        {
            using var alcance = fabricaDeAlcances.CreateScope();
            var bitacora = alcance.ServiceProvider.GetRequiredService<IBitacoraDeConsultas>();
            await bitacora.RegistrarAsync(evento, cancelacion);
        }
        catch (Exception ex)
        {
            registro.LogWarning(ex, "No se pudo registrar la consulta en la bitácora.");
        }
    }
}

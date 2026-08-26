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
            copiloto.Publicado += (_, evento) => canal.Publicar(evento);
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
}

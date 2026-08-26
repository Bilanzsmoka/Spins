using PokerProOS.Application.Bitacora;
using PokerProOS.Application.Voz;

namespace PokerProOS.Api.Voz;

/// <summary>
/// Enciende el copiloto al arrancar la aplicación. Si el motor de voz no
/// está disponible, la aplicación sigue funcionando sin voz: las tablas
/// se consultan igual desde la pantalla.
/// </summary>
/// <remarks>
/// <see cref="CopilotoDeVoz"/> y <see cref="IReconocedorDeVoz"/> se resuelven
/// acá adentro, en <see cref="ExecuteAsync"/>, y no se toman por constructor.
/// El host resuelve los servicios hospedados (llamando al constructor) antes
/// de arrancarlos, fuera de cualquier try nuestro: si el grafo se construyera
/// ahí, un <c>ReconocedorSapi</c> que falla al compilar la gramática de
/// <c>vocabulario.json</c> (o un <c>SintetizadorSapi</c> que no encuentra la
/// voz configurada) tumbaría <c>Host.StartAsync</c> entero, sin tablas y sin
/// diagnóstico. Resolviéndolos acá, esa misma falla cae en el catch de abajo,
/// que ya sabe convertirla en <see cref="Falla"/>.
/// </remarks>
public sealed class ServicioDeCopiloto(
    IServiceProvider proveedorDeServicios,
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
            var copiloto = proveedorDeServicios.GetRequiredService<CopilotoDeVoz>();
            var reconocedor = proveedorDeServicios.GetRequiredService<IReconocedorDeVoz>();

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

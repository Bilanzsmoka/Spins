using System.Threading.Channels;
using PokerProOS.Application.Voz;

namespace PokerProOS.Api.Voz;

/// <summary>
/// Reparte los eventos del copiloto a cada navegador conectado por SSE.
/// Cada suscriptor tiene su propio canal acotado: si uno se atrasa, se le
/// descarta el evento más viejo en vez de frenar al resto.
/// </summary>
public sealed class CanalDeEventos
{
    private readonly List<Channel<EventoDeCopiloto>> _suscriptores = [];
    private readonly Lock _candado = new();

    public EventoDeCopiloto? Ultimo { get; private set; }

    /// <summary>
    /// Se llama desde el hilo de callback de SAPI: no puede lanzar bajo
    /// ninguna circunstancia, o la excepción escapa hacia el reconocedor y
    /// tumba el bucle de escucha. TryWrite sobre un canal acotado no lanza.
    /// </summary>
    public void Publicar(EventoDeCopiloto evento)
    {
        Ultimo = evento;
        lock (_candado)
            foreach (var canal in _suscriptores)
                canal.Writer.TryWrite(evento);
    }

    public (ChannelReader<EventoDeCopiloto> Lector, IDisposable Suscripcion) Suscribir()
    {
        var canal = Channel.CreateBounded<EventoDeCopiloto>(
            new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.DropOldest });

        lock (_candado) _suscriptores.Add(canal);

        return (canal.Reader, new Baja(this, canal));
    }

    private sealed class Baja(CanalDeEventos canal, Channel<EventoDeCopiloto> propio) : IDisposable
    {
        public void Dispose()
        {
            lock (canal._candado) canal._suscriptores.Remove(propio);
            propio.Writer.TryComplete();
        }
    }
}

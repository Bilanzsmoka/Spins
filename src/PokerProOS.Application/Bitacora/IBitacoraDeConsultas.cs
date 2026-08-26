using PokerProOS.Application.Voz;

namespace PokerProOS.Application.Bitacora;

public interface IBitacoraDeConsultas
{
    Task RegistrarAsync(EventoDeCopiloto evento, CancellationToken cancelacion);
}

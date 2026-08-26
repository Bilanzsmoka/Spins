using PokerProOS.Domain.Diario;

namespace PokerProOS.Application.Diario;

/// <summary>Resumen automático del día, calculado de la bitácora de voz.</summary>
public record ResumenDelDia(
    int Consultas,
    int Resueltas,
    IReadOnlyList<ManoConsultada> ManosMasConsultadas,
    string? PrimeraHora,
    string? UltimaHora);

public record ManoConsultada(string Mano, string Accion, int Veces);

public interface IRepositorioDeDiario
{
    Task<EntradaDeDiario?> ObtenerAsync(DateOnly fecha, CancellationToken ct);
    Task<IReadOnlyList<EntradaDeDiario>> ListarAsync(int limite, CancellationToken ct);
    Task<EntradaDeDiario> GuardarAsync(EntradaDeDiario entrada, CancellationToken ct);
    Task<ResumenDelDia> ResumirAsync(DateOnly fecha, CancellationToken ct);
}

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

/// <summary>
/// Lo que te propusiste ayer y cómo salió. Es el bucle que pidió el usuario:
/// el objetivo de un día solo sirve si al día siguiente alguien te lo recuerda
/// y te dice cómo te fue.
/// </summary>
public record Comparativa(
    DateOnly? FechaPrevia,
    string? ObjetivoPrevio,
    int? CumplimientoPrevio,
    string? NivelPrevio,
    int? VolumenPrevio,
    int? VolumenDeHoy,
    int ConsultasPrevias,
    int ConsultasDeHoy);

public interface IRepositorioDeDiario
{
    Task<EntradaDeDiario?> ObtenerAsync(DateOnly fecha, CancellationToken ct);
    Task<IReadOnlyDictionary<string, int>> MarcasAsync(DateOnly fecha, CancellationToken ct);
    Task<IReadOnlyDictionary<string, string>> NotasDeHabitosAsync(DateOnly fecha, CancellationToken ct);
    Task GuardarMarcasAsync(
        DateOnly fecha,
        IReadOnlyDictionary<string, int> marcas,
        IReadOnlyDictionary<string, string> notas,
        CancellationToken ct);
    Task<Comparativa> CompararAsync(DateOnly fecha, CancellationToken ct);
    Task<ProgresoDeHabitos> ProgresoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    Task<IReadOnlyList<EntradaDeDiario>> ListarAsync(int limite, CancellationToken ct);
    Task<EntradaDeDiario> GuardarAsync(EntradaDeDiario entrada, CancellationToken ct);
    Task<ResumenDelDia> ResumirAsync(DateOnly fecha, CancellationToken ct);
}

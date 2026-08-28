using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// Junta el puerto y el planificador: lee lo vencido y lo ya conocido, y le
/// pide la tanda al planificador, que es donde vive la regla.
/// </summary>
public sealed class ArmarTandaHandler(
    IProgresoDeEntrenamiento progreso,
    PlanificadorDeTanda planificador)
{
    public async Task<IReadOnlyList<PreguntaDeTanda>> ArmarAsync(
        int usuarioId, FiltroDeTanda filtro, int tamano, DateOnly hoy, CancellationToken ct)
    {
        var vencidas = await progreso.VencidasAsync(usuarioId, hoy, ct);
        var todas = await progreso.TodasAsync(usuarioId, ct);

        return planificador.Planificar(
            vencidas,
            todas.Select(t => t.ClaveDeCasilla()).ToList(),
            filtro,
            tamano);
    }
}

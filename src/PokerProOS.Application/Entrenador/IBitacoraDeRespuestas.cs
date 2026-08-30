using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// Dónde queda registrada cada respuesta. Sólo escribe: por ahora el dato se
/// junta y nada lo lee todavía.
///
/// Que empiece a existir es lo que desbloquea todo lo demás —el mapa de
/// errores, la curva de velocidad, el dominio medido en tiempo—, y ninguna de
/// esas cosas se puede evaluar antes de tener meses de historial. Por eso se
/// guarda ahora y se usa después.
/// </summary>
public interface IBitacoraDeRespuestas
{
    Task RegistrarAsync(RespuestaRegistrada respuesta, CancellationToken ct);
}

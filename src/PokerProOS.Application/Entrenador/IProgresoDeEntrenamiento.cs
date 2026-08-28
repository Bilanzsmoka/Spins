using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// El progreso, sin decir dónde vive. Es el único puerto del entrenador que
/// necesita base: todo lo demás sale del catálogo en memoria.
/// </summary>
public interface IProgresoDeEntrenamiento
{
    /// <summary>Las casillas cuyo día ya llegó, de más vencida a menos.</summary>
    Task<IReadOnlyList<ProgresoDeCasilla>> VencidasAsync(
        int usuarioId, DateOnly hoy, CancellationToken ct);

    /// <summary>
    /// Todo lo que esta persona alguna vez contestó. Sirve para una sola cosa:
    /// que el material nuevo no repita casillas ya estudiadas.
    /// </summary>
    Task<IReadOnlyList<ProgresoDeCasilla>> TodasAsync(int usuarioId, CancellationToken ct);

    Task<ProgresoDeCasilla?> BuscarAsync(
        int usuarioId, string situacion, string claveDeStack, string spot, string mano,
        CancellationToken ct);

    Task GuardarAsync(ProgresoDeCasilla progreso, CancellationToken ct);
}

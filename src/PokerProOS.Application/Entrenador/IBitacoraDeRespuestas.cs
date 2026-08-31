using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// Una equivocación que se repite: la misma casilla contestada con la misma
/// acción equivocada, más de una vez.
///
/// Se agrupa por la acción elegida y no sólo por la casilla porque el error
/// tiene forma: no es lo mismo tirar A5s siempre que subirla de más a veces.
/// Lo que se repite es lo que hay que desarmar.
/// </summary>
public record ErrorRepetido(
    string Situacion,
    string ClaveDeStack,
    string Spot,
    string Mano,
    string AccionElegida,
    string AccionCorrecta,
    int Veces,
    DateTime Ultima);

/// <summary>
/// Dónde queda registrada cada respuesta, y de dónde sale lo que se aprende de
/// ellas.
///
/// El registro es el hecho crudo; lo que se lee son patrones. La curva de
/// velocidad y el dominio medido en tiempo todavía no se pueden calcular —
/// necesitan meses de historial—, pero el mapa de errores empieza a servir con
/// dos repeticiones.
/// </summary>
public interface IBitacoraDeRespuestas
{
    Task RegistrarAsync(RespuestaRegistrada respuesta, CancellationToken ct);

    /// <summary>
    /// Lo que más veces erraste igual, de más a menos. Sólo lo que se repitió:
    /// una equivocación suelta es ruido, y el valor está en el patrón.
    /// </summary>
    Task<IReadOnlyList<ErrorRepetido>> ErroresRepetidosAsync(
        int usuarioId, int cuantos, CancellationToken ct);
}

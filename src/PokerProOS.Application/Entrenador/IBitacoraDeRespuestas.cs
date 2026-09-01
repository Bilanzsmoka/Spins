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
/// <summary>
/// Cómo venís en un spot: cuántas contestaste, cuántas acertaste y cuánto
/// tardás en promedio.
///
/// El spot y no la casilla: una mano suelta que fallaste no dice nada, pero un
/// spot con 60% de aciertos sobre cuarenta manos es una tabla que no sabés, y
/// eso sí se puede ir a entrenar.
/// </summary>
public record RendimientoDeSpot(
    string Situacion,
    string ClaveDeStack,
    string Spot,
    int Respondidas,
    int Aciertos,
    int MilisegundosPromedio)
{
    public int Porcentaje => Respondidas == 0 ? 0 : (int)(100.0 * Aciertos / Respondidas);
}

/// <summary>Lo tuyo, en total.</summary>
public record RendimientoTotal(
    int Respondidas,
    int Aciertos,
    int MilisegundosPromedio,
    IReadOnlyList<RendimientoDeSpot> PeoresSpots)
{
    public int Porcentaje => Respondidas == 0 ? 0 : (int)(100.0 * Aciertos / Respondidas);
}

public interface IBitacoraDeRespuestas
{
    Task RegistrarAsync(RespuestaRegistrada respuesta, CancellationToken ct);

    /// <summary>
    /// Lo que más veces erraste igual, de más a menos. Sólo lo que se repitió:
    /// una equivocación suelta es ruido, y el valor está en el patrón.
    /// </summary>
    Task<IReadOnlyList<ErrorRepetido>> ErroresRepetidosAsync(
        int usuarioId, int cuantos, CancellationToken ct);

    /// <summary>
    /// Cuánto llevás contestado, cuánto acertaste, y los spots que peor te
    /// salen — de menor a mayor porcentaje.
    /// </summary>
    /// <param name="minimo">
    /// Cuántas respuestas tiene que tener un spot para entrar en la lista. Con
    /// dos respuestas, un fallo da 50% y encabezaría la lista sin significar
    /// nada; lo que se busca son tablas que no sabés, no mala suerte.
    /// </param>
    Task<RendimientoTotal> RendimientoAsync(
        int usuarioId, int cuantosSpots, int minimo, CancellationToken ct);
}

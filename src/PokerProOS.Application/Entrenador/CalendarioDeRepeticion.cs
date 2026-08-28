namespace PokerProOS.Application.Entrenador;

/// <summary>Cómo queda una casilla después de contestarla.</summary>
public record ProgresoCalculado(int AciertosSeguidos, int IntervaloEnDias, DateOnly Vence);

/// <summary>
/// Cuándo vuelve a preguntarse una casilla.
///
/// Puro a propósito: sin base, sin catálogo y sin reloj propio —la fecha entra
/// como parámetro—. Así la regla se prueba entera con cuatro tests y no hay
/// forma de que el resultado dependa del día en que se corren.
/// </summary>
public static class CalendarioDeRepeticion
{
    /// <summary>
    /// Los descansos, en días. Cada acierto sube un escalón y el último se
    /// repite para siempre: una casilla que se sabe hace tres meses no
    /// necesita desaparecer, solo aparecer poco.
    /// </summary>
    public static IReadOnlyList<int> Escalera { get; } = [1, 3, 7, 16, 35, 90];

    public static ProgresoCalculado Siguiente(int aciertosSeguidos, bool acerto, DateOnly hoy)
    {
        // Fallar no baja un escalón: vuelve a cero. Y vence HOY, no mañana,
        // porque el spec pide que la casilla reentre en la tanda actual — es
        // el momento en que más sirve volver a verla.
        if (!acerto) return new ProgresoCalculado(0, Escalera[0], hoy);

        var nuevos = aciertosSeguidos + 1;
        var intervalo = Escalera[Math.Min(nuevos - 1, Escalera.Count - 1)];
        return new ProgresoCalculado(nuevos, intervalo, hoy.AddDays(intervalo));
    }
}

namespace PokerProOS.Domain.Tablas;

/// <summary>Una parte de una estrategia mixta: qué acción y con qué frecuencia.</summary>
public record ParteDeMix(string Accion, int Frecuencia);

/// <summary>
/// Una mano con lo que la tabla prescribe. Casi siempre es una acción pura,
/// pero algunas manos son mixtas: la tabla dice hacer dos cosas distintas
/// repartidas por frecuencia.
/// </summary>
/// <param name="Accion">
/// La acción dominante. En un mix es la de mayor frecuencia; si empatan, la
/// primera declarada. Existe para que todo lo que solo necesita "qué hago
/// acá" siga funcionando sin saber de mixes.
/// </param>
/// <param name="Mix">
/// Las partes, cuando la mano es mixta. Nulo en una mano pura — no una lista
/// de un elemento, para que <see cref="EsMixta"/> sea inequívoco.
/// </param>
public record CeldaDeTabla(string Mano, string Accion, IReadOnlyList<ParteDeMix>? Mix = null)
{
    public bool EsMixta => Mix is { Count: > 1 };
}

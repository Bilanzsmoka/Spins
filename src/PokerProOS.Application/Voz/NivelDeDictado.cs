namespace PokerProOS.Application.Voz;

/// <summary>
/// Los escalones de una consulta, en el orden en que se arman: qué mesa, qué
/// tabla, cuántas fichas, en qué punto de la mano, y recién ahí las cartas.
///
/// Nombrar uno al empezar la frase le dice al intérprete contra qué categoría
/// buscar, y ese es todo el punto: sin la etiqueta, "tres max" (el formato) se
/// come el "tres" que era el rango, y "be be contra limp" (la situación) se
/// come el "contra limp" que era el spot. Las claves de los dichos en
/// <c>vocabulario.json</c> son estos nombres.
/// </summary>
public enum NivelDeDictado
{
    Formato,
    Situacion,
    Stack,
    Spot,
    Mano,
}

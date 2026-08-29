namespace PokerProOS.Application.Tablas;

/// <summary>
/// Cuánta baraja se lleva una acción. En combos, no en casillas: una casilla
/// suited son cuatro manos reales y una offsuit son doce, así que contar
/// casillas exagera lo suited justo donde uno quiere calcular.
/// </summary>
/// <param name="Combos">
/// Fraccionario porque una celda mixta reparte sus combos entre sus acciones
/// según la frecuencia declarada.
/// </param>
public record PesoDeAccion(string Accion, double Combos, double PorcentajeDeBaraja);

/// <summary>
/// El bloque contiguo de una familia que comparte una acción, y la mano que lo
/// rompe. Es la forma de acordarse de un rango sin memorizar mano por mano:
/// alcanza con el fondo del bloque.
/// </summary>
/// <param name="Familia">Notación de póker: "Axs", "Axo", "Pares".</param>
/// <param name="Tope">La mano más alta del bloque.</param>
/// <param name="Fondo">La más baja: la mano ancla.</param>
/// <param name="Siguiente">
/// La primera que ya no entra, o nulo si el bloque llega al final de la familia.
/// </param>
public record AnclaDeFamilia(
    string Familia,
    string Tope,
    string Fondo,
    string Accion,
    string? Siguiente,
    string? AccionSiguiente);

/// <summary>Un tramo de stacks donde la mano hace siempre lo mismo.</summary>
/// <param name="ClaveDeStack">
/// La clave del stack, o "{primero}…{ultimo}" si la banda junta varios.
/// </param>
/// <param name="EsElActual">
/// Si el stack consultado cae adentro de esta banda. Se marca acá y no se
/// deduce comparando claves en la pantalla: una banda que junta varios
/// stacks no lleva la clave de ninguno de ellos, así que la comparación
/// fallaría justo cuando el stack que estás jugando quedó fusionado.
/// </param>
public record BandaDeStack(
    string ClaveDeStack, decimal MinBB, decimal MaxBB, string Accion, bool EsElActual);

/// <summary>Un spot del stack y lo que esa mano hace ahí.</summary>
public record PasoDeLinea(string Spot, string Etiqueta, string Accion, bool EsElConsultado);

/// <summary>
/// Todo lo que se puede decir de una mano en un spot sin inventar nada: cinco
/// piezas deducidas del catálogo y el tip escrito a mano, si lo hay.
/// </summary>
public record FichaDeMemoria(
    string Mano,
    string Accion,
    string ClaveDeStack,
    IReadOnlyList<PesoDeAccion> Pesos,
    AnclaDeFamilia? Ancla,
    IReadOnlyList<BandaDeStack> Umbral,
    IReadOnlyList<AnclaDeFamilia> Familias,
    IReadOnlyList<PasoDeLinea> Linea,
    string? Tip,
    /// <summary>
    /// El spot contado en pocas frases: "todos los Ax son ALL-IN", "los Kx
    /// offsuit hasta K7o". Es lo que de verdad se memoriza — nadie retiene
    /// 169 casillas, retiene el grupo y dónde corta.
    /// </summary>
    IReadOnlyList<ReglaDelSpot> Reglas);

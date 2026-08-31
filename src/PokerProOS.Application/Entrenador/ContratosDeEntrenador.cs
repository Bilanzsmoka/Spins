using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Tablas;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// Sobre qué entrenar. Todo opcional: sin nada elegido entra el catálogo
/// entero. El rango de stack va en BB y se compara contra la cobertura real de
/// cada tabla, no contra su clave.
/// </summary>
public record FiltroDeTanda(
    string? Formato, string? Situacion, decimal? MinBB, decimal? MaxBB, string? Spot);

/// <summary>
/// Una pregunta de la tanda. Trae las etiquetas ya resueltas porque la
/// pantalla las muestra y pedírselas al catálogo otra vez sería un segundo
/// viaje para un dato que acá está a mano.
/// </summary>
public record PreguntaDeTanda(
    string Situacion,
    string EtiquetaDeSituacion,
    string ClaveDeStack,
    string Spot,
    string EtiquetaDeSpot,
    string Mano,
    /// <summary>Material nuevo, sin progreso previo. La pantalla lo distingue.</summary>
    bool EsNueva);

/// <summary>Lo que la pantalla manda al contestar.</summary>
/// <param name="Milisegundos">
/// Cuánto tardaste desde que apareció la pregunta. Con valor por defecto
/// porque no todos los caminos lo miden todavía, y porque una respuesta sin
/// tiempo tiene que seguir contando: perder el acierto por no haber medido
/// sería peor que no medir.
/// </param>
public record RespuestaEnviada(
    string Situacion, string ClaveDeStack, string Spot, string Mano, string Accion,
    int Milisegundos = 0);

/// <summary>
/// Qué pasó con la respuesta. La ficha viene solo al fallar: acertar sigue de
/// largo, y es al errar cuando una explicación entra de verdad.
/// </summary>
/// <param name="Cerca">
/// Erró, pero por una acción vecina en la escala de agresión. Se dice en
/// pantalla porque no da lo mismo: saber que erraste el tamaño y no el spot es
/// la mitad de la corrección.
/// </param>
public record VeredictoDeRespuesta(
    bool Acerto,
    string AccionCorrecta,
    IReadOnlyList<ParteDeMix>? Mix,
    FichaDeMemoria? Ficha,
    DateOnly Vence,
    bool Cerca = false);

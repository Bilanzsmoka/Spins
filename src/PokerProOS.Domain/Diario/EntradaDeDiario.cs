namespace PokerProOS.Domain.Diario;

/// <summary>
/// Una entrada por día. Los campos cortos existen para poder cruzarlos
/// después (¿cuántas sesiones jugué en C-game este mes? ¿qué disparador se
/// repite?); las notas son texto libre y son lo que el usuario realmente
/// escribe. Todo es opcional salvo la fecha: un diario que exige llenar
/// formularios se abandona en una semana.
/// </summary>
public class EntradaDeDiario
{
    public int Id { get; set; }

    /// <summary>Día al que corresponde la entrada. Uno por día.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>La única intención de la sesión, escrita antes de jugar.</summary>
    public string? Intencion { get; set; }

    /// <summary>Autocalificación: "A", "B" o "C". Nulo si no se calificó.</summary>
    public string? NivelDeJuego { get; set; }

    /// <summary>Qué disparó el tilt o la frustración. El disparador, no el tilt.</summary>
    public string? Disparador { get; set; }

    public int? Mesas { get; set; }
    public int? Minutos { get; set; }

    /// <summary>
    /// Lo que te propusiste técnicamente antes de jugar. Distinto de
    /// <see cref="Intencion"/>: la intención es de conducta ("no pagar sin
    /// blockers"), el objetivo técnico es medible ("bajar el VPIP a 38").
    /// </summary>
    public string? ObjetivoTecnico { get; set; }

    /// <summary>Qué tan bien cumpliste el objetivo técnico, de 1 a 10.</summary>
    public int? CumplimientoObjetivo { get; set; }

    /// <summary>El cuerpo del diario. Lo que pasó, en sus palabras.</summary>
    public string Notas { get; set; } = string.Empty;

    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadaEn { get; set; } = DateTime.UtcNow;
}

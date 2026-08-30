namespace PokerProOS.Domain.Entrenador;

/// <summary>
/// Una respuesta, tal como ocurrió: qué casilla era, qué contestaste, qué
/// decía la tabla y <b>cuánto tardaste</b>.
///
/// Es el hecho crudo, y va aparte de <see cref="ProgresoDeCasilla"/> a
/// propósito: aquél guarda el estado —en qué escalón está una casilla— y se
/// pisa cada vez que contestás. Éste no se pisa nunca. Sin historial no se
/// puede saber <b>qué</b> errás repetido ni si estás contestando más rápido que
/// el mes pasado, y ésas son las dos preguntas que separan saber una tabla de
/// tenerla como reflejo.
/// </summary>
public class RespuestaRegistrada
{
    public int Id { get; set; }

    /// <summary>Igual que el progreso: en la clave desde el día uno, aunque no haya login.</summary>
    public int UsuarioId { get; set; }

    public string Situacion { get; set; } = "";
    public string ClaveDeStack { get; set; } = "";
    public string Spot { get; set; } = "";
    public string Mano { get; set; } = "";

    public string AccionElegida { get; set; } = "";

    /// <summary>
    /// Lo que decía la tabla en ese momento. Se guarda aunque se pueda
    /// recalcular: si la tabla se corrige después, el registro tiene que seguir
    /// contando lo que realmente pasó, no lo que hoy sería correcto.
    /// </summary>
    public string AccionCorrecta { get; set; } = "";

    public bool Acerto { get; set; }

    /// <summary>
    /// Desde que la pregunta apareció hasta que contestaste. Cero cuando el
    /// cliente no lo mandó: un dato viejo o una respuesta por voz que no lo
    /// midió no puede inventarse un tiempo, y contarlo como rápido sería peor
    /// que no contarlo.
    /// </summary>
    public int Milisegundos { get; set; }

    public DateTime RespondidaEn { get; set; } = DateTime.UtcNow;
}

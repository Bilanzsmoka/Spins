namespace PokerProOS.Application.Plan;

/// <summary>
/// Un objetivo con nombre, un número y una barra. Uno se cumple y se prende el
/// siguiente.
///
/// Hay dos tipos y no más: <c>saber</c> apunta a una situación del catálogo y
/// lo mide el entrenador; <c>jugar</c> apunta a un hábito numérico y lo miden
/// las marcas del día. Cuál de los dos es decide qué campos se leen, así que
/// un hito trae los de su tipo y nulos en los del otro.
/// </summary>
/// <param name="Situacion">Sólo en los de <c>saber</c>: la clave de la tabla.</param>
/// <param name="EscalonMinimo">
/// Sólo en los de <c>saber</c>: a partir de qué descanso —en días, de la
/// escalera de repetición— una casilla cuenta como sabida. 16 son cuatro
/// aciertos seguidos separados en el tiempo, que no se finge.
/// </param>
/// <param name="Habito">Sólo en los de <c>jugar</c>: la clave del hábito numérico.</param>
/// <param name="Dias">Sólo en los de <c>jugar</c>: cuántos días atrás se mira.</param>
/// <param name="Objetivo">
/// En <c>saber</c>, el porcentaje de bordes que hay que tener. En <c>jugar</c>,
/// el número que hay que alcanzar cada día.
/// </param>
public record HitoDefinido(
    string Clave,
    string Titulo,
    string Tipo,
    int Objetivo,
    string? Situacion = null,
    int EscalonMinimo = 0,
    string? Habito = null,
    int Dias = 0);

/// <summary>
/// El plan entero, en el orden en que se recorre.
/// </summary>
/// <param name="HabitoDeVolumen">
/// Qué hábito cuenta los torneos jugados, y cuál el estudio del día. Salen del
/// JSON y no de una constante: los hábitos ya viven en datos, y clavarle acá
/// la clave "VOLUMEN" haría que renombrar un hábito rompiera el plan en
/// silencio.
/// </param>
public record PlanDefinido(
    int MetaDeVolumen,
    string HabitoDeVolumen,
    string HabitoDeEstudio,
    IReadOnlyList<HitoDefinido> Hitos)
{
    public static PlanDefinido Vacio { get; } = new(0, "", "", []);

    public bool HayPlan => Hitos.Count > 0;
}

/// <summary>
/// El plan leído de <c>database/registro/plan.json</c>.
///
/// Como el glosario y a diferencia de las acciones: si falta o está mal, la
/// app arranca igual y la pantalla no muestra panel. Un plan de estudio es
/// material del usuario, no configuración de la que dependa servir una tabla.
/// </summary>
public interface IRegistroDelPlan
{
    PlanDefinido Plan { get; }
}

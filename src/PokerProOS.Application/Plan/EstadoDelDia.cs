namespace PokerProOS.Application.Plan;

/// <summary>
/// Un hito con cuánto lleva. <paramref name="Hecho"/> y
/// <paramref name="Total"/> viajan además del porcentaje porque el número
/// crudo —"241 de 357 bordes"— es lo que impide leer el porcentaje como algo
/// que no es.
/// </summary>
/// <param name="Situacion">
/// La tabla a la que apunta, para que el botón de entrenar pueda filtrar la
/// tanda. Nula en los hitos de jugar.
/// </param>
/// <param name="Problema">
/// Por qué no se pudo medir: apunta a una situación o a un hábito que no
/// existe, o su tipo no se entiende. Un hito roto se muestra con su causa y no
/// frena a los demás, igual que una tabla que no valida.
/// </param>
public record EstadoDeHito(
    string Clave,
    string Titulo,
    string Tipo,
    int Hecho,
    int Total,
    int Porcentaje,
    int Objetivo,
    bool Cumplido,
    bool EsElActivo,
    string? Situacion = null,
    string? Problema = null);

/// <summary>Un día de la tira de la semana.</summary>
public record DiaDelPlan(DateOnly Fecha, int Volumen, bool Alcanzo, bool EsHoy);

/// <summary>
/// Todo lo que la pantalla de hoy necesita, ya resuelto.
/// </summary>
/// <param name="SinDosSeguidos">
/// Si NO hay dos días seguidos por debajo de la meta. Es la regla que
/// reemplaza a la racha: medir días seguidos hace abandonar el hábito entero
/// al primer fallo, y "nunca dos seguidos" es la que se sostiene.
/// </param>
/// <param name="SituacionQueToca">
/// La tabla del primer hito de saber sin cumplir, para el botón de entrenar.
/// Va aparte del hito activo porque el activo puede ser uno de jugar —sostener
/// el volumen dos semanas—, y durante esos días igual hay que poder entrenar.
/// </param>
public record EstadoDelDia(
    int MetaDeVolumen,
    int VolumenDeHoy,
    bool EstudioHecho,
    IReadOnlyList<EstadoDeHito> Hitos,
    IReadOnlyList<DiaDelPlan> Semana,
    bool SinDosSeguidos,
    string? SituacionQueToca);

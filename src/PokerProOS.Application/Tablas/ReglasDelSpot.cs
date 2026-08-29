namespace PokerProOS.Application.Tablas;

/// <summary>
/// Una frase corta que describe qué hace un grupo entero de manos.
/// </summary>
/// <param name="Grupo">Cómo se llama el grupo: "los Ax offsuit", "los pares".</param>
/// <param name="Accion">Lo que la tabla dice para ese grupo.</param>
/// <param name="Hasta">
/// La mano más baja que todavía hace <see cref="Accion"/>, cuando el grupo se
/// corta. Nula si el grupo entero comparte la acción.
///
/// Se dice el fondo y no el rango completo —"hasta K7o" y no "de KQo a K7o"—
/// porque el tope se deduce solo y el corte es lo único que hay que recordar.
/// Es la técnica de la mano ancla.
/// </param>
/// <param name="Despues">Lo que se hace de <see cref="Hasta"/> para abajo. Nula si no se corta.</param>
/// <param name="Manos">Cuántas manos cubre la frase, para ordenar por kilometraje.</param>
public record ReglaDelSpot(string Grupo, string Accion, string? Hasta, string? Despues, int Manos);

/// <summary>
/// Lee un spot y lo dice en pocas frases: "todos los Ax son ALL-IN", "los Kx
/// offsuit: ALL-IN hasta K7o, de ahí CALL".
///
/// Es la tabla contada, no una opinión sobre ella: cada frase se deduce de lo
/// que el archivo declara. Por eso no puede contradecir a quien armó las
/// tablas, que es justamente el riesgo de estudiar material ajeno.
///
/// La regla que lo hace confiable: <b>un grupo se nombra sólo si comprime</b>.
/// Si comparte una acción, o si se parte en dos bloques contiguos, se dice. Si
/// se parte en tres pedazos, se calla — decir "los broadways son ALL-IN" donde
/// la mitad no lo son sería enseñar algo falso, que es peor que no enseñar
/// nada.
/// </summary>
public static class ReglasDelSpot
{
    public static IReadOnlyList<ReglaDelSpot> De(SpotDeTabla spot, int cuantas)
    {
        var reglas = new List<ReglaDelSpot>();

        foreach (var grupo in GruposDeManos.Todos)
            if (Leer(spot, grupo) is { } regla)
                reglas.Add(regla);

        // Por cuántas manos cubre: la frase que más tabla explica primero.
        return reglas
            .OrderByDescending(r => r.Manos)
            .ThenBy(r => r.Grupo, StringComparer.Ordinal)
            .Take(cuantas)
            .ToList();
    }

    private static ReglaDelSpot? Leer(SpotDeTabla spot, GrupoDeManos grupo)
    {
        var manos = grupo.Manos.Where(m => spot.AccionDe(m) is not null).ToList();
        if (manos.Count < 2) return null;

        var primera = spot.AccionDe(manos[0])!;
        var corte = manos.FindIndex(m => !Mismo(spot.AccionDe(m), primera));

        // Todo el grupo hace lo mismo: la frase más útil que existe.
        if (corte < 0)
            return new ReglaDelSpot(grupo.Nombre, primera, null, null, manos.Count);

        // Se corta: sólo sirve si lo que sigue es todo igual. Si vuelve a
        // partirse, la frase prometería más de lo que la tabla dice.
        var despues = spot.AccionDe(manos[corte])!;
        var resto = manos.Skip(corte);
        if (!resto.All(m => Mismo(spot.AccionDe(m), despues))) return null;

        return new ReglaDelSpot(grupo.Nombre, primera, manos[corte - 1], despues, corte);
    }

    private static bool Mismo(string? a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

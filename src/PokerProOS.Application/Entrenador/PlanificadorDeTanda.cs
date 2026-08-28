using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// Arma la tanda: lo vencido primero y, si sobra lugar, material nuevo.
///
/// Puro: recibe las vencidas ya leídas y no toca la base. Lo que sí necesita
/// es el catálogo, porque una casilla vencida puede haber dejado de existir
/// —las tablas se corrigen a mano— y porque el material nuevo sale de ahí.
///
/// El orden del relleno es determinista, no al azar. Hay más de 57.000
/// casillas y al azar no se cubren nunca; además, una vez contestada, una
/// casilla deja de ser nueva, así que el recorrido avanza solo.
/// </summary>
public sealed class PlanificadorDeTanda(ICatalogoDeTablas catalogo)
{
    public IReadOnlyList<PreguntaDeTanda> Planificar(
        IReadOnlyList<ProgresoDeCasilla> vencidas,
        IReadOnlyCollection<string> yaConocidas,
        FiltroDeTanda filtro,
        int tamano)
    {
        if (tamano <= 0) return [];

        var elegidas = new List<PreguntaDeTanda>();

        foreach (var vencida in vencidas.OrderBy(v => v.Vence).ThenBy(v => v.Mano))
        {
            if (elegidas.Count == tamano) break;
            if (Pregunta(vencida, filtro) is { } pregunta) elegidas.Add(pregunta);
        }

        if (elegidas.Count == tamano) return elegidas;

        // El relleno no puede repetir ni lo ya estudiado ni lo que acaba de
        // entrar por vencido.
        var vistas = new HashSet<string>(yaConocidas, StringComparer.OrdinalIgnoreCase);
        foreach (var p in elegidas)
            vistas.Add(ProgresoDeCasilla.Clave(p.Situacion, p.ClaveDeStack, p.Spot, p.Mano));

        foreach (var nueva in Nuevas(filtro, vistas))
        {
            if (elegidas.Count == tamano) break;
            elegidas.Add(nueva);
        }

        return elegidas;
    }

    /// <summary>
    /// La vencida convertida en pregunta, o null si el filtro la deja afuera o
    /// si su casilla ya no existe en el catálogo. Progreso huérfano no es un
    /// error: un spot puede desaparecer al corregir una tabla.
    /// </summary>
    private PreguntaDeTanda? Pregunta(ProgresoDeCasilla vencida, FiltroDeTanda filtro)
    {
        var situacion = catalogo.Situacion(vencida.Situacion);
        if (situacion is null || !PasaSituacion(situacion, filtro)) return null;

        var tabla = catalogo.StackPorClave(vencida.Situacion, vencida.ClaveDeStack);
        if (tabla is null || !PasaStack(tabla, filtro)) return null;

        var spot = tabla.Spot(vencida.Spot);
        if (spot is null || !PasaSpot(spot, filtro)) return null;
        if (spot.AccionDe(vencida.Mano) is null) return null;

        return new PreguntaDeTanda(
            situacion.Clave, situacion.Etiqueta,
            tabla.Stack.Clave,
            spot.Clave, spot.Etiqueta,
            vencida.Mano,
            EsNueva: false);
    }

    /// <summary>
    /// Material nuevo, con los bordes adelante. Un borde es donde se corta el
    /// bloque de una familia o cambia el umbral de stack: son las casillas que
    /// separan saber la tabla de adivinarla. El resto va después para que la
    /// tanda igual se llene cuando los bordes se agotan.
    /// </summary>
    private IEnumerable<PreguntaDeTanda> Nuevas(FiltroDeTanda filtro, HashSet<string> vistas)
    {
        var candidatas = new List<(bool Borde, PreguntaDeTanda Pregunta)>();

        foreach (var situacion in catalogo.Situaciones)
        {
            if (!PasaSituacion(situacion, filtro)) continue;

            foreach (var tabla in situacion.Stacks)
            {
                if (!PasaStack(tabla, filtro)) continue;

                foreach (var spot in tabla.Spots)
                {
                    if (!PasaSpot(spot, filtro)) continue;

                    foreach (var celda in spot.Celdas)
                    {
                        var clave = ProgresoDeCasilla.Clave(
                            situacion.Clave, tabla.Stack.Clave, spot.Clave, celda.Mano);
                        if (!vistas.Add(clave)) continue;

                        candidatas.Add((
                            spot.EnElBorde(celda.Mano),
                            new PreguntaDeTanda(
                                situacion.Clave, situacion.Etiqueta,
                                tabla.Stack.Clave,
                                spot.Clave, spot.Etiqueta,
                                celda.Mano,
                                EsNueva: true)));
                    }
                }
            }
        }

        return candidatas.OrderByDescending(c => c.Borde).Select(c => c.Pregunta);
    }

    private static bool PasaSituacion(SituacionDeTabla situacion, FiltroDeTanda filtro)
        => (filtro.Formato is not { Length: > 0 }
            || string.Equals(situacion.Formato, filtro.Formato, StringComparison.OrdinalIgnoreCase))
           && (filtro.Situacion is not { Length: > 0 }
            || string.Equals(situacion.Clave, filtro.Situacion, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// El filtro de stack se compara contra la cobertura de la tabla, no
    /// contra su clave: entra toda tabla cuya banda se toque con la pedida.
    /// </summary>
    private static bool PasaStack(TablaDeStack tabla, FiltroDeTanda filtro)
        => (filtro.MinBB is not { } min || tabla.Stack.MaxBB >= min)
           && (filtro.MaxBB is not { } max || tabla.Stack.MinBB <= max);

    private static bool PasaSpot(SpotDeTabla spot, FiltroDeTanda filtro)
        => filtro.Spot is not { Length: > 0 }
           || string.Equals(spot.Clave, filtro.Spot, StringComparison.OrdinalIgnoreCase);
}

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
    /// <summary>
    /// Cuántas de la misma página se toleran aunque la tanda sea chica.
    ///
    /// Lo que hace daño no es que dos o tres seguidas compartan tabla: es la
    /// racha larga, donde la respuesta empieza a salir de la pregunta anterior.
    /// Y en una tanda de tres, apretar el tope obligaría a meter material nuevo
    /// desplazando algo que ya venció — que es peor negocio que la racha.
    /// </summary>
    private const int RachaTolerable = 3;

    public IReadOnlyList<PreguntaDeTanda> Planificar(
        IReadOnlyList<ProgresoDeCasilla> vencidas,
        IReadOnlyCollection<string> yaConocidas,
        FiltroDeTanda filtro,
        int tamano)
    {
        if (tamano <= 0) return [];

        // Lo vencido, en orden de urgencia, y repartido entre páginas: si una
        // tabla se estudió entera hace una semana, TODAS sus casillas vencen
        // juntas y la tanda salía siendo diez casillas del mismo spot. La
        // urgencia sigue mandando —las páginas entran en el orden de su casilla
        // más vencida—, pero se toma de a una por página, por vuelta.
        // Lo más vencido primero; entre las que vencen el mismo día, al azar.
        // Desempatar por el nombre de la mano ordenaba alfabéticamente, y en la
        // matriz eso es siempre "AA" adelante.
        var porUrgencia = vencidas
            .OrderBy(v => v.Vence).ThenBy(_ => Random.Shared.Next())
            .Select(v => Pregunta(v, filtro))
            .OfType<PreguntaDeTanda>()
            .ToList();

        // Ninguna página se lleva más de la mitad de la tanda. Estudiar una
        // tabla entera un día hace que TODAS sus casillas venzan el mismo día,
        // y sin este tope la tanda de mañana son diez seguidas de ese spot —
        // que es exactamente la práctica agrupada que no queremos. Lo que no
        // entra no se pierde: sigue vencido y entra en la próxima.
        var elegidas = DeAUnaPorPagina(porUrgencia, tamano, Math.Max(RachaTolerable, tamano / 2));

        if (elegidas.Count == tamano) return Repartir(elegidas);

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

        // Si ni con material nuevo se llena, entra lo vencido que el tope había
        // dejado afuera: repartir nunca puede devolver una tanda más corta que
        // el material disponible.
        foreach (var vencida in porUrgencia)
        {
            if (elegidas.Count == tamano) break;
            if (!elegidas.Contains(vencida)) elegidas.Add(vencida);
        }

        return Repartir(elegidas);
    }

    /// <summary>
    /// Toma hasta <paramref name="tamano"/> preguntas rotando entre páginas: la
    /// primera de cada página, después la segunda de cada una, y así.
    ///
    /// El orden en que aparecen las páginas es el de prioridad —la primera
    /// página es la de la pregunta más urgente—, así que rotar no le saca
    /// urgencia a nada: sólo evita que una sola página se lleve la tanda
    /// entera. Y si hay una sola página, salen todas de ahí: no poder repartir
    /// nunca puede devolver menos preguntas de las que había.
    /// </summary>
    /// <param name="topePorPagina">Cuántas puede aportar una misma página.</param>
    private static List<PreguntaDeTanda> DeAUnaPorPagina(
        IEnumerable<PreguntaDeTanda> porPrioridad, int tamano, int topePorPagina)
    {
        var paginas = new List<Queue<PreguntaDeTanda>>();
        var porClave = new Dictionary<string, Queue<PreguntaDeTanda>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pregunta in porPrioridad)
        {
            if (!porClave.TryGetValue(Pagina(pregunta), out var cola))
            {
                cola = new Queue<PreguntaDeTanda>();
                porClave[Pagina(pregunta)] = cola;
                paginas.Add(cola);
            }
            cola.Enqueue(pregunta);
        }

        var elegidas = new List<PreguntaDeTanda>(tamano);
        for (var vuelta = 0; vuelta < topePorPagina && elegidas.Count < tamano; vuelta++)
            foreach (var pagina in paginas)
            {
                if (elegidas.Count == tamano) break;
                if (pagina.Count > 0) elegidas.Add(pagina.Dequeue());
            }

        return elegidas;
    }

    /// <summary>
    /// Las mismas preguntas, repartidas para que dos seguidas casi nunca sean
    /// de la misma página de la tabla.
    ///
    /// Practicar agrupado —diez seguidas del mismo spot— <b>hace rendir mejor
    /// durante la tanda y peor a la semana siguiente</b>. Con el bloque
    /// delante, la respuesta sale de la pregunta anterior y no de la memoria,
    /// que es justo lo que no se quiere entrenar: en la mesa las manos no
    /// vienen ordenadas por tabla.
    ///
    /// Reparte, no baraja. La prioridad decide <b>quién entra</b> a la tanda y
    /// eso no se toca: lo más vencido sigue primero. Esto decide sólo
    /// <b>en qué orden se pregunta</b>, y es determinista —el de más prioridad
    /// que no repita página— para que la tanda sea reproducible y las pruebas
    /// puedan fijarla.
    /// </summary>
    private static IReadOnlyList<PreguntaDeTanda> Repartir(List<PreguntaDeTanda> elegidas)
    {
        if (elegidas.Count < 3) return elegidas;

        var restantes = new LinkedList<PreguntaDeTanda>(elegidas);
        var repartidas = new List<PreguntaDeTanda>(elegidas.Count);
        string? anterior = null;

        while (restantes.First is not null)
        {
            // Si todas las que quedan repiten página, va la de más prioridad:
            // no poder repartir no puede achicar la tanda ni cambiar qué entra.
            var elegido = restantes.First;
            for (var nodo = restantes.First; nodo is not null; nodo = nodo.Next)
                if (Pagina(nodo.Value) != anterior)
                {
                    elegido = nodo;
                    break;
                }

            repartidas.Add(elegido.Value);
            anterior = Pagina(elegido.Value);
            restantes.Remove(elegido);
        }

        return repartidas;
    }

    /// <summary>
    /// La página que estarías mirando: una situación, un stack y un spot. Es
    /// el bloque que hay que romper — no la situación sola, porque dos stacks
    /// de la misma tabla ya son dos decisiones distintas.
    /// </summary>
    private static string Pagina(PreguntaDeTanda p)
        => $"{p.Situacion}|{p.ClaveDeStack}|{p.Spot}";

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

        // Bordes primero, y rotando entre páginas. Ordenar sólo por borde
        // agotaba una tabla entera antes de pasar a la siguiente: una tanda de
        // diez salía siendo diez casillas del mismo spot, y repartir el orden
        // después no puede arreglar una selección que ya es toda de la misma
        // página. El índice es la posición dentro de su propia página, así que
        // ordenar por él reparte de a una por página, por vuelta.
        //
        // Y dentro de cada página va al azar. Recorrer la matriz en su orden
        // natural hacía que la primera casilla de TODAS las páginas fuera "AA":
        // rotando entre páginas, la tanda salía siendo el mismo as repetido en
        // cinco tablas. Un patrón así se aprende antes que las tablas —
        // contestás de memoria la secuencia, no la mano— y es justo lo que la
        // recuperación activa necesita que no pase.
        //
        // No rompe la cobertura: lo ya contestado deja de ser material nuevo,
        // así que el recorrido igual avanza hasta agotar el catálogo. Lo único
        // que cambia es en qué orden.
        return candidatas
            .GroupBy(c => (Pagina(c.Pregunta), c.Borde))
            .SelectMany(grupo => grupo
                .OrderBy(_ => Random.Shared.Next())
                .Select((c, indice) => (c.Borde, Indice: indice, c.Pregunta)))
            .OrderByDescending(c => c.Borde)
            .ThenBy(c => c.Indice)
            .Select(c => c.Pregunta);
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

using PokerProOS.Domain.Manos;

namespace PokerProOS.Application.Tablas;

/// <summary>
/// Explica una mano en vez de sólo responderla. Deduce todo del catálogo en
/// memoria: no guarda nada propio, así que una tabla corregida cambia la
/// explicación en el acto.
/// </summary>
public sealed class AnalizadorDeMemoria(ICatalogoDeTablas catalogo)
{
    public FichaDeMemoria? Analizar(
        string situacion, string claveDeStack, string claveDeSpot, string mano)
    {
        var spot = catalogo.Spot(situacion, claveDeStack, claveDeSpot);
        var celda = spot?.CeldaDe(mano);
        if (spot is null || celda is null) return null;

        return new FichaDeMemoria(
            celda.Mano,
            celda.Accion,
            claveDeStack,
            Pesos(spot),
            Ancla(spot, celda.Mano),
            Umbral(situacion, claveDeStack, claveDeSpot, celda.Mano),
            [],
            [],
            null);
    }

    /// <summary>
    /// Una celda mixta reparte sus combos entre sus acciones según la
    /// frecuencia: contarla entera en las dos haría que los porcentajes
    /// sumaran más de 100 y el número dejaría de significar "de la baraja".
    /// </summary>
    private static IReadOnlyList<PesoDeAccion> Pesos(SpotDeTabla spot)
    {
        var combos = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var celda in spot.Celdas)
        {
            var deLaCelda = MatrizDeManos.Combos(celda.Mano);
            if (celda.Mix is { Count: > 1 } partes)
                foreach (var parte in partes)
                    Sumar(parte.Accion, deLaCelda * parte.Frecuencia / 100.0);
            else
                Sumar(celda.Accion, deLaCelda);
        }

        return combos
            .OrderByDescending(par => par.Value)
            .Select(par => new PesoDeAccion(
                par.Key, par.Value, par.Value * 100.0 / MatrizDeManos.CombosTotales))
            .ToList();

        void Sumar(string accion, double cuantos)
            => combos[accion] = combos.GetValueOrDefault(accion) + cuantos;
    }

    /// <summary>
    /// La familia de una mano, ordenada de mayor a menor: los pares, o el
    /// rango alto contra cada kicker. Es el eje por el que se recuerda un
    /// rango — "hasta A9o" dice más que trece manos sueltas.
    /// </summary>
    private static (string Nombre, List<string> Manos) Familia(string mano)
    {
        if (mano.Length == 2)
            return ("Pares", MatrizDeManos.Rangos.Select(r => $"{r}{r}").ToList());

        var alto = mano[0];
        var palo = mano[2];
        var manos = MatrizDeManos.Rangos
            .Skip(MatrizDeManos.IndiceDeRango(alto) + 1)
            .Select(bajo => $"{alto}{bajo}{palo}")
            .ToList();
        return ($"{alto}x{palo}", manos);
    }

    /// <summary>
    /// El bloque contiguo de la familia que contiene a la mano y comparte su
    /// acción. Si la familia entera hace lo mismo no hay nada que anclar: el
    /// ancla existe para marcar dónde se corta.
    /// </summary>
    private static AnclaDeFamilia? Ancla(SpotDeTabla spot, string mano)
    {
        var (nombre, familia) = Familia(mano);
        var accion = spot.AccionDe(mano);
        if (accion is null) return null;

        var indice = familia.IndexOf(mano);
        if (indice < 0) return null;

        var desde = indice;
        while (desde > 0 && Igual(spot.AccionDe(familia[desde - 1]), accion)) desde--;

        var hasta = indice;
        while (hasta < familia.Count - 1 && Igual(spot.AccionDe(familia[hasta + 1]), accion)) hasta++;

        if (desde == 0 && hasta == familia.Count - 1) return null;

        var siguiente = hasta < familia.Count - 1 ? familia[hasta + 1] : null;
        return new AnclaDeFamilia(
            nombre,
            familia[desde],
            familia[hasta],
            accion,
            siguiente,
            siguiente is null ? null : spot.AccionDe(siguiente));
    }

    private static bool Igual(string? a, string? b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// La misma mano a lo largo de todos los stacks de la situación,
    /// colapsada en tramos de igual acción. Es la forma en que se estudian
    /// estos rangos: no trece tablas sueltas, sino dos o tres cortes.
    /// </summary>
    private IReadOnlyList<BandaDeStack> Umbral(
        string situacion, string claveDeStack, string claveDeSpot, string mano)
    {
        var stacks = catalogo.Situacion(situacion)?.Stacks;
        if (stacks is null) return [];

        var bandas = new List<BandaDeStack>();
        foreach (var tabla in stacks)
        {
            var accion = tabla.Spot(claveDeSpot)?.AccionDe(mano);
            if (accion is null) continue;

            var esElActual = Igual(tabla.Stack.Clave, claveDeStack);
            var ultima = bandas.Count > 0 ? bandas[^1] : null;

            // Se extiende sólo si el stack anterior pega con éste: un stack
            // sin este spot corta el tramo, porque entre medio la tabla no
            // dice nada y fingir continuidad sería inventar.
            var continua = ultima is not null
                && Igual(ultima.Accion, accion)
                && ultima.MaxBB == tabla.Stack.MinBB - 1;

            if (continua)
                bandas[^1] = ultima! with
                {
                    ClaveDeStack = Unir(ultima.ClaveDeStack, tabla.Stack.Clave),
                    MaxBB = tabla.Stack.MaxBB,
                    // La banda es la actual si CUALQUIERA de los stacks que
                    // absorbió lo es, no sólo el primero.
                    EsElActual = ultima.EsElActual || esElActual,
                };
            else
                bandas.Add(new BandaDeStack(
                    tabla.Stack.Clave, tabla.Stack.MinBB, tabla.Stack.MaxBB, accion, esElActual));
        }
        return bandas;
    }

    /// <summary>
    /// El nombre de una banda que abarca varios stacks: sus extremos. Se
    /// recorta lo ya unido para que tres stacks no den "a…b…c".
    /// </summary>
    private static string Unir(string acumulado, string ultimo)
    {
        var primero = acumulado.Split('…')[0];
        return primero == ultimo ? ultimo : $"{primero}…{ultimo}";
    }
}

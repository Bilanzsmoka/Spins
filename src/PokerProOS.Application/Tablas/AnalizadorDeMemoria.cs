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
            null,
            [],
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
}

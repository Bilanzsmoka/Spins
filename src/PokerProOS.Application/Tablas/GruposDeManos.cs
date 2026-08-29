using PokerProOS.Domain.Manos;

namespace PokerProOS.Application.Tablas;

/// <summary>Un conjunto de manos que se nombra de una, y cómo se lo llama.</summary>
public record GrupoDeManos(string Nombre, IReadOnlyList<string> Manos);

/// <summary>
/// Los grupos con los que se memoriza una tabla.
///
/// Nadie memoriza 169 casillas: memoriza "todos los pares", "todos los Ax",
/// "los broadways". Es la técnica que usa todo el mundo para aprender rangos,
/// y estos son sus nombres.
///
/// Todos se derivan de la matriz —por índice de rango—, ninguno es una lista
/// escrita a mano: agregar un rango cambiaría los grupos solo. Los 13 rangos
/// son la única constante que el proyecto permite, y viven en MatrizDeManos.
/// </summary>
public static class GruposDeManos
{
    /// <summary>Hasta dónde llegan las cartas altas: A, K, Q, J y T.</summary>
    private const int UltimoBroadway = 4;

    public static IReadOnlyList<GrupoDeManos> Todos { get; } = Construir();

    private static List<GrupoDeManos> Construir()
    {
        var rangos = MatrizDeManos.Rangos;
        var grupos = new List<GrupoDeManos>
        {
            new("los pares", rangos.Select(r => $"{r}{r}").ToList()),
        };

        // Por cada carta alta, su fila suited y su fila offsuit: los Ax, los
        // Kx… El último rango no abre fila porque no tiene nada más bajo.
        for (var alto = 0; alto < rangos.Count - 1; alto++)
        {
            var bajos = rangos.Skip(alto + 1).ToList();
            grupos.Add(new GrupoDeManos(
                $"los {rangos[alto]}x suited",
                bajos.Select(b => $"{rangos[alto]}{b}s").ToList()));
            grupos.Add(new GrupoDeManos(
                $"los {rangos[alto]}x offsuit",
                bajos.Select(b => $"{rangos[alto]}{b}o").ToList()));
        }

        var broadways = new List<string>();
        var conectores = new List<string>();
        var unGappers = new List<string>();

        for (var alto = 0; alto < rangos.Count; alto++)
            for (var bajo = alto + 1; bajo < rangos.Count; bajo++)
            {
                var par = $"{rangos[alto]}{rangos[bajo]}";
                if (alto <= UltimoBroadway && bajo <= UltimoBroadway)
                {
                    broadways.Add($"{par}s");
                    broadways.Add($"{par}o");
                }
                if (bajo - alto == 1) conectores.Add($"{par}s");
                if (bajo - alto == 2) unGappers.Add($"{par}s");
            }

        grupos.Add(new GrupoDeManos("los broadways", broadways));
        grupos.Add(new GrupoDeManos("los suited connectors", conectores));
        grupos.Add(new GrupoDeManos("los one-gappers suited", unGappers));
        return grupos;
    }
}

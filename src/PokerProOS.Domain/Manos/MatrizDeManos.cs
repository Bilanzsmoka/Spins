namespace PokerProOS.Domain.Manos;

/// <summary>
/// La matriz canónica de 13x13 manos iniciales de Hold'em.
/// Los 13 rangos son la única constante legítima del proyecto: el póker no cambia.
/// </summary>
public static class MatrizDeManos
{
    public static IReadOnlyList<char> Rangos { get; } =
        ['A', 'K', 'Q', 'J', 'T', '9', '8', '7', '6', '5', '4', '3', '2'];

    private static readonly IReadOnlyList<string> _todas = Construir();

    public static IReadOnlyList<string> Todas() => _todas;

    public static string Etiqueta(int fila, int columna)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fila);
        ArgumentOutOfRangeException.ThrowIfNegative(columna);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fila, Rangos.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(columna, Rangos.Count);

        var alto = Rangos[Math.Min(fila, columna)];
        var bajo = Rangos[Math.Max(fila, columna)];
        if (fila == columna) return $"{alto}{bajo}";
        return fila < columna ? $"{alto}{bajo}s" : $"{alto}{bajo}o";
    }

    public static IReadOnlyList<string> Vecinas(string etiqueta)
    {
        var (fila, columna) = Coordenadas(etiqueta);
        var vecinas = new List<string>(4);
        foreach (var (df, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            var f = fila + df;
            var c = columna + dc;
            if (f < 0 || c < 0 || f >= Rangos.Count || c >= Rangos.Count) continue;
            vecinas.Add(Etiqueta(f, c));
        }
        return vecinas;
    }

    private static (int Fila, int Columna) Coordenadas(string etiqueta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(etiqueta);
        var primero = IndiceDeRango(etiqueta[0]);
        var segundo = IndiceDeRango(etiqueta[1]);
        if (primero < 0 || segundo < 0)
            throw new ArgumentException($"Mano desconocida: {etiqueta}", nameof(etiqueta));

        if (etiqueta.Length == 2) return (primero, primero);
        return etiqueta[2] switch
        {
            's' => (primero, segundo),
            'o' => (segundo, primero),
            _ => throw new ArgumentException($"Mano desconocida: {etiqueta}", nameof(etiqueta))
        };
    }

    /// <summary>
    /// Posición de un rango dentro de <see cref="Rangos"/>. Búsqueda lineal porque
    /// IReadOnlyList&lt;char&gt; no expone IndexOf (eso es de IList&lt;T&gt;), y no
    /// vale la pena envolver los 13 rangos en otro tipo solo por ese método.
    /// </summary>
    public static int IndiceDeRango(char rango)
    {
        for (var i = 0; i < Rangos.Count; i++)
            if (Rangos[i] == rango) return i;
        return -1;
    }

    private static List<string> Construir()
    {
        var manos = new List<string>(169);
        for (var fila = 0; fila < Rangos.Count; fila++)
            for (var columna = 0; columna < Rangos.Count; columna++)
                manos.Add(Etiqueta(fila, columna));
        return manos.Distinct().ToList();
    }
}

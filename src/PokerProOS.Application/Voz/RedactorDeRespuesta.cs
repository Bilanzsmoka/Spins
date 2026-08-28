using PokerProOS.Application.Tablas;

namespace PokerProOS.Application.Voz;

/// <summary>
/// Arma la frase que se va a hablar. La regla del spec: acción sola cuando no
/// hay nada que aclarar, y repetir la mano solo cuando se asumió el palo, que
/// es cuando pudo haberse perdido la palabra "suited" en el reconocimiento.
/// </summary>
public sealed class RedactorDeRespuesta(IRegistroDeAcciones acciones, IRegistroDeVocabulario vocabulario)
{
    /// <summary>
    /// La confirmación de un dictado de contexto. Repite solo lo que el
    /// dictado cambió: sin esto no hay forma de saber si la orden entró, y
    /// repetir las tres piezas cada vez sería más largo que la consulta.
    /// Las palabras salen de la forma canónica del vocabulario —el primer
    /// dicho—, no de literales, para que cambiar el JSON cambie lo que se oye.
    /// </summary>
    public string RedactarContexto(
        string? situacion, decimal? stackBB, string? spot, string? formato = null)
    {
        var piezas = new List<string>();
        if (formato is { Length: > 0 }) piezas.Add(Canonico(vocabulario.Formatos, formato));
        if (situacion is { Length: > 0 }) piezas.Add(Canonico(vocabulario.Situaciones, situacion));
        if (stackBB is { } bb) piezas.Add($"{bb:0.##} {PalabraDeStack()}");
        if (spot is { Length: > 0 }) piezas.Add(Canonico(vocabulario.Spots, spot));

        return piezas.Count == 0 ? "Listo." : $"{string.Join(", ", piezas)}.";
    }

    /// <summary>
    /// Lo que se dice cuando la frase no era una orden. Vive acá y no en el
    /// controlador porque es texto hablado, igual que el resto: quien lo
    /// quiera cambiar tiene un solo lugar donde buscarlo.
    /// </summary>
    public string RedactarNoEntendido() => NoEntendi;

    private const string NoEntendi = "No te entendí.";

    private string PalabraDeStack() => vocabulario.PalabrasDeStack.FirstOrDefault() ?? "be be";

    private static string Canonico(IReadOnlyList<FormasHabladas> formas, string clave)
        => formas.FirstOrDefault(f =>
               string.Equals(f.Clave, clave, StringComparison.OrdinalIgnoreCase))
           ?.Dichos.FirstOrDefault() ?? clave;

    public string Redactar(ResultadoDeConsulta resultado)
    {
        if (resultado.Respuesta is null)
            return resultado.Detalle ?? NoEntendi;

        var r = resultado.Respuesta;
        var etiqueta = acciones.Existe(r.Accion) ? acciones.Obtener(r.Accion).Etiqueta : r.Accion;

        // Una mano mixta se dice como mix: mientras jugas, lo que necesitas
        // saber es que la tabla NO tiene una respuesta unica ahi, no el
        // numero exacto. Decir "cincuenta por ciento" en cada consulta seria
        // mas largo y menos util que decir que esta repartida.
        if (r.Mix is { Count: > 1 } partes)
        {
            var reparto = string.Join(", ", partes.Select(p =>
                $"{Frecuencia(p.Frecuencia)} {Etiqueta(p.Accion)}"));
            return r.PaloAsumido
                ? $"{Deletrear(r.Mano)}: mix, {reparto}."
                : $"Mix: {reparto}.";
        }

        var frase = r.PaloAsumido
            ? $"{Deletrear(r.Mano)}: {etiqueta}."
            : $"{etiqueta}.";

        return frase;
    }

    /// <summary>
    /// Separa la mano para que la síntesis no lea "AKo" como una palabra. La palabra
    /// del palo sale del registro de vocabulario (su forma canónica, el primer dicho),
    /// no de un literal, para que cambiar el JSON cambie lo que se dice.
    /// </summary>
    private string Etiqueta(string clave)
        => acciones.Existe(clave) ? acciones.Obtener(clave).Etiqueta : clave;

    /// <summary>
    /// "mitad" suena mejor hablado que "cincuenta por ciento", y el reparto
    /// más común con diferencia es el 50/50.
    /// </summary>
    private static string Frecuencia(int porcentaje)
        => porcentaje == 50 ? "mitad" : $"{porcentaje} por ciento";

    private string Deletrear(string mano)
    {
        var rangos = $"{mano[0]} {mano[1]}";
        if (mano.Length == 2) return rangos;

        var claveDePalo = mano[2].ToString();
        var palabraDePalo = vocabulario.Palos
            .FirstOrDefault(p => p.Clave == claveDePalo)?.Dichos.FirstOrDefault();

        return palabraDePalo is null ? rangos : $"{rangos} {palabraDePalo}";
    }
}

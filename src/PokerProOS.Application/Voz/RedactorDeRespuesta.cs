using PokerProOS.Application.Tablas;

namespace PokerProOS.Application.Voz;

/// <summary>
/// Arma la frase que se va a hablar. La regla del spec: acción sola cuando no
/// hay nada que aclarar, y repetir la mano solo cuando se asumió el palo, que
/// es cuando pudo haberse perdido la palabra "suited" en el reconocimiento.
/// </summary>
public sealed class RedactorDeRespuesta(IRegistroDeAcciones acciones, IRegistroDeVocabulario vocabulario)
{
    public string Redactar(ResultadoDeConsulta resultado)
    {
        if (resultado.Respuesta is null)
            return resultado.Detalle ?? "No te entendí.";

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

        if (r.EnElBorde)
            frase += $" En el borde, {r.ManosEnLaAccion} manos.";

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

using PokerProOS.Application.Tablas;

namespace PokerProOS.Application.Voz;

/// <summary>
/// Arma la frase que se va a hablar. La regla del spec: acción sola cuando no
/// hay nada que aclarar, y repetir la mano solo cuando se asumió el palo, que
/// es cuando pudo haberse perdido la palabra "suited" en el reconocimiento.
/// </summary>
public sealed class RedactorDeRespuesta(IRegistroDeAcciones acciones)
{
    public string Redactar(ResultadoDeConsulta resultado)
    {
        if (resultado.Respuesta is null)
            return resultado.Detalle ?? "No te entendí.";

        var r = resultado.Respuesta;
        var etiqueta = acciones.Existe(r.Accion) ? acciones.Obtener(r.Accion).Etiqueta : r.Accion;

        var frase = r.PaloAsumido
            ? $"{Deletrear(r.Mano)}: {etiqueta}."
            : $"{etiqueta}.";

        if (r.EnElBorde)
            frase += $" En el borde, {r.ManosEnLaAccion} manos.";

        return frase;
    }

    /// <summary>Separa la mano para que la síntesis no lea "AKo" como una palabra.</summary>
    private static string Deletrear(string mano)
    {
        var rangos = $"{mano[0]} {mano[1]}";
        if (mano.Length == 2) return rangos;
        return mano[2] == 's' ? $"{rangos} suited" : $"{rangos} offsuit";
    }
}

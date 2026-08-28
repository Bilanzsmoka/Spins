using PokerProOS.Domain.Manos;

namespace PokerProOS.Application.Tablas;

public sealed class ResolverManoHandler(ICatalogoDeTablas catalogo)
{
    public ResultadoDeConsulta Resolver(ConsultaDeMano consulta)
    {
        if (catalogo.Situacion(consulta.Situacion) is null)
            return Sin(MotivoSinRespuesta.SituacionDesconocida,
                $"No conozco la situación {consulta.Situacion}.");

        var tabla = catalogo.StackQueCubre(consulta.Situacion, consulta.StackBB);
        if (tabla is null)
            return Sin(MotivoSinRespuesta.StackFueraDeCobertura,
                $"No tengo tabla para {consulta.StackBB} be be.");

        var spot = tabla.Spot(consulta.Spot);
        if (spot is null)
            return Sin(MotivoSinRespuesta.SpotInexistente,
                $"Ese spot no existe a {tabla.Stack.Clave}.");

        var compuesto = Componer(consulta);
        if (compuesto is null)
            return Sin(MotivoSinRespuesta.ManoInvalida,
                $"No reconozco el rango dictado: '{consulta.RangoAlto}' / '{consulta.RangoBajo}'.");

        var (mano, paloAsumido) = compuesto.Value;
        var accion = spot.AccionDe(mano);
        if (accion is null)
            return Sin(MotivoSinRespuesta.ManoInvalida, $"No reconozco la mano {mano}.");

        var celda = spot.CeldaDe(mano);

        var enElBorde = spot.EnElBorde(mano);

        return new ResultadoDeConsulta(
            new RespuestaDeMano(
                mano,
                accion,
                spot.Conteos.GetValueOrDefault(accion),
                enElBorde,
                paloAsumido,
                tabla.Stack.Clave,
                celda?.EsMixta == true ? celda.Mix : null),
            null,
            null);
    }

    /// <summary>
    /// Ordena los rangos de mayor a menor y aplica la regla del spec:
    /// una mano dictada sin palo es offsuit, salvo que sea pareja.
    /// Devuelve null si algún rango no es reconocible: el spec no adivina
    /// la mano más parecida, así que esto se resuelve como ManoInvalida,
    /// no como una excepción.
    /// </summary>
    private static (string Mano, bool PaloAsumido)? Componer(ConsultaDeMano consulta)
    {
        if (string.IsNullOrEmpty(consulta.RangoAlto) || string.IsNullOrEmpty(consulta.RangoBajo))
            return null;

        // MatrizDeManos.IndiceDeRango, no Rangos.IndexOf: IReadOnlyList<char>
        // no expone IndexOf, eso es de IList<T>. Devuelve -1 si el caracter
        // no es uno de los 13 rangos.
        var indiceAlto = MatrizDeManos.IndiceDeRango(consulta.RangoAlto[0]);
        var indiceBajo = MatrizDeManos.IndiceDeRango(consulta.RangoBajo[0]);
        if (indiceAlto < 0 || indiceBajo < 0)
            return null;

        var alto = MatrizDeManos.Rangos[Math.Min(indiceAlto, indiceBajo)];
        var bajo = MatrizDeManos.Rangos[Math.Max(indiceAlto, indiceBajo)];

        if (alto == bajo) return ($"{alto}{bajo}", false);

        var palo = consulta.Palo;
        var asumido = string.IsNullOrEmpty(palo);
        return ($"{alto}{bajo}{(asumido ? "o" : palo!.ToLowerInvariant())}", asumido);
    }

    private static ResultadoDeConsulta Sin(MotivoSinRespuesta motivo, string detalle)
        => new(null, motivo, detalle);
}

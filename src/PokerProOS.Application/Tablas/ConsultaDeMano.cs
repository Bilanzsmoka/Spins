using PokerProOS.Domain.Tablas;

namespace PokerProOS.Application.Tablas;

public record ConsultaDeMano(
    string Situacion,
    decimal StackBB,
    string Spot,
    string RangoAlto,
    string RangoBajo,
    string? Palo);

public record RespuestaDeMano(
    string Mano,
    string Accion,
    int ManosEnLaAccion,
    bool EnElBorde,
    bool PaloAsumido,
    string ClaveDeStack,
    /// <summary>
    /// Las partes cuando la tabla prescribe una estrategia mixta. Nulo si la
    /// mano es pura. <see cref="Accion"/> sigue trayendo la dominante.
    /// </summary>
    IReadOnlyList<ParteDeMix>? Mix = null);

public enum MotivoSinRespuesta
{
    SituacionDesconocida,
    StackFueraDeCobertura,
    SpotInexistente,
    ManoInvalida
}

public record ResultadoDeConsulta(
    RespuestaDeMano? Respuesta,
    MotivoSinRespuesta? Motivo,
    string? Detalle);

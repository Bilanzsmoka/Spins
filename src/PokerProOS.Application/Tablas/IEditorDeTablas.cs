using PokerProOS.Domain.Tablas;

namespace PokerProOS.Application.Tablas;

/// <summary>Lo que se quiere dejar en una celda: una acción pura o un mix.</summary>
public record EdicionDeCelda(
    string Situacion,
    string ClaveDeStack,
    string Spot,
    string Mano,
    string? Accion,
    IReadOnlyList<ParteDeMix>? Mix);

public record ResultadoDeEdicion(bool Exito, string? Error, IReadOnlyList<ProblemaDeTabla> Problemas);

/// <summary>
/// El porqué escrito a mano de un spot. Texto vacío o nulo borra la clave del
/// archivo: un "tip" en blanco es un problema de tabla, no un tip.
/// </summary>
public record EdicionDeTip(string Situacion, string ClaveDeStack, string Spot, string? Texto);

/// <summary>
/// Edita el archivo JSON, que es la fuente de verdad, y recarga el catálogo.
/// No toca la base: la base es un espejo que se rehace del JSON al arrancar.
/// </summary>
public interface IEditorDeTablas
{
    Task<ResultadoDeEdicion> EditarAsync(EdicionDeCelda edicion, CancellationToken ct);
    Task<ResultadoDeEdicion> EditarTipAsync(EdicionDeTip edicion, CancellationToken ct);
}

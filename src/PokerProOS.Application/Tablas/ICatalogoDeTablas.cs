using PokerProOS.Domain.Tablas;

namespace PokerProOS.Application.Tablas;

public record SpotDeTabla(
    string Clave,
    string Etiqueta,
    IReadOnlyList<CeldaDeTabla> Celdas,
    /// <summary>
    /// El porqué escrito a mano: lo único de la ficha que ningún cálculo puede
    /// deducir de la tabla. Nulo si el spot no lo declara.
    /// </summary>
    string? Tip = null)
{
    private readonly Dictionary<string, string> _porMano =
        Celdas.ToDictionary(c => c.Mano, c => c.Accion, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> Conteos { get; } = Celdas
        .GroupBy(c => c.Accion, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, CeldaDeTabla> _celdaPorMano =
        Celdas.ToDictionary(c => c.Mano, c => c, StringComparer.OrdinalIgnoreCase);

    public string? AccionDe(string mano) => _porMano.GetValueOrDefault(mano);

    /// <summary>La celda completa, con su mix si lo tiene.</summary>
    public CeldaDeTabla? CeldaDe(string mano) => _celdaPorMano.GetValueOrDefault(mano);
}

public record TablaDeStack(RangoDeStack Stack, IReadOnlyList<SpotDeTabla> Spots)
{
    public SpotDeTabla? Spot(string clave) =>
        Spots.FirstOrDefault(s => string.Equals(s.Clave, clave, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// El formato de mesa al que pertenece la situación ("HU", "3-max"). Lo
/// declara el archivo, no lo deduce el código de la clave: agregar un formato
/// nuevo tiene que ser dejar un JSON, igual que agregar una tabla.
/// </summary>
public record SituacionDeTabla(
    string Clave, string Etiqueta, string Formato, IReadOnlyList<TablaDeStack> Stacks);

public interface ICatalogoDeTablas
{
    IReadOnlyList<SituacionDeTabla> Situaciones { get; }
    IReadOnlyList<ProblemaDeTabla> Problemas { get; }
    SituacionDeTabla? Situacion(string clave);
    TablaDeStack? StackQueCubre(string situacion, decimal bb);
    TablaDeStack? StackPorClave(string situacion, string claveStack);
    SpotDeTabla? Spot(string situacion, string claveStack, string claveSpot);
}

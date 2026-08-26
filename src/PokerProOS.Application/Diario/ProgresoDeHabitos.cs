namespace PokerProOS.Application.Diario;

/// <summary>Un día de la grilla: qué se marcó y cómo se jugó.</summary>
public record DiaDeGrilla(
    DateOnly Fecha,
    string? NivelDeJuego,
    IReadOnlyDictionary<string, int> Marcas,
    IReadOnlyDictionary<string, string> Notas);

/// <summary>Cumplimiento de un hábito en el período.</summary>
public record ResumenDeHabito(
    string Clave,
    int Cumplidos,
    int DiasRegistrados,
    int RachaActual,
    int MejorRacha);

/// <summary>
/// El cruce que hace útil el seguimiento: cómo jugaste los días que hiciste
/// el hábito contra los días que no. <paramref name="Confiable"/> es falso
/// cuando hay tan pocos días de un lado que el número no significa nada —
/// mostrar un porcentaje sacado de dos días sería mentir con estadística.
/// </summary>
public record CruceDeHabito(
    string Clave,
    int DiasCon,
    int BuenosCon,
    int DiasSin,
    int BuenosSin,
    bool Confiable);

public record ProgresoDeHabitos(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<DiaDeGrilla> Dias,
    IReadOnlyList<ResumenDeHabito> Resumen,
    IReadOnlyList<CruceDeHabito> Cruces);

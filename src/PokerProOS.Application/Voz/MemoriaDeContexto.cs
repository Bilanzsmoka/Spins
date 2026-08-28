namespace PokerProOS.Application.Voz;

/// <summary>
/// Guarda el stack y el spot activos para que no haya que repetirlos en cada
/// consulta. Si el dictado los trae, se actualizan; si no, se reutilizan.
/// </summary>
public sealed class MemoriaDeContexto
{
    public string Situacion { get; set; } = "";
    public decimal StackBB { get; set; }
    public string Spot { get; set; } = "";

    public void Aplicar(DictadoReconocido dictado)
    {
        if (dictado.Situacion is { Length: > 0 } situacion) Situacion = situacion;
        if (dictado.StackBB is { } stack) StackBB = stack;
        if (dictado.Spot is { Length: > 0 } spot) Spot = spot;
        // El formato NO se guarda acá: no es un dato más de la memoria, es un
        // pedido de mudarse a otra tabla. Quien tiene el catálogo para elegir a
        // cuál es el copiloto.
    }
}

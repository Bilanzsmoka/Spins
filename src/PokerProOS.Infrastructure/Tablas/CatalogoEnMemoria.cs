using PokerProOS.Application.Tablas;

namespace PokerProOS.Infrastructure.Tablas;

public sealed class CatalogoEnMemoria(
    IReadOnlyList<SituacionDeTabla> situaciones,
    IReadOnlyList<ProblemaDeTabla> problemas) : ICatalogoDeTablas
{
    public IReadOnlyList<SituacionDeTabla> Situaciones { get; } = situaciones;
    public IReadOnlyList<ProblemaDeTabla> Problemas { get; } = problemas;

    public SituacionDeTabla? Situacion(string clave) =>
        Situaciones.FirstOrDefault(s => string.Equals(s.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public TablaDeStack? StackQueCubre(string situacion, decimal bb) =>
        Situacion(situacion)?.Stacks.FirstOrDefault(t => t.Stack.Cubre(bb));

    public TablaDeStack? StackPorClave(string situacion, string claveStack) =>
        Situacion(situacion)?.Stacks.FirstOrDefault(
            t => string.Equals(t.Stack.Clave, claveStack, StringComparison.OrdinalIgnoreCase));

    public SpotDeTabla? Spot(string situacion, string claveStack, string claveSpot) =>
        StackPorClave(situacion, claveStack)?.Spot(claveSpot);
}

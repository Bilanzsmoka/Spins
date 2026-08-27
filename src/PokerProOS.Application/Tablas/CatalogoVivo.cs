namespace PokerProOS.Application.Tablas;

/// <summary>
/// Un catálogo que se puede reemplazar en caliente. El editor reescribe el
/// JSON y llama a <see cref="Reemplazar"/>; todo lo que tenga inyectado
/// <see cref="ICatalogoDeTablas"/> ve los datos nuevos sin reiniciar.
/// </summary>
/// <remarks>
/// La lectura no se sincroniza a propósito: reemplazar es un cambio de
/// referencia, que es atómico. Un lector puede quedarse con la versión
/// anterior por un instante, y para consultar una tabla eso da igual.
/// </remarks>
public sealed class CatalogoVivo(ICatalogoDeTablas inicial) : ICatalogoDeTablas
{
    private ICatalogoDeTablas _actual = inicial;

    public void Reemplazar(ICatalogoDeTablas nuevo) => _actual = nuevo;

    public IReadOnlyList<SituacionDeTabla> Situaciones => _actual.Situaciones;
    public IReadOnlyList<ProblemaDeTabla> Problemas => _actual.Problemas;
    public SituacionDeTabla? Situacion(string clave) => _actual.Situacion(clave);
    public TablaDeStack? StackQueCubre(string situacion, decimal bb) => _actual.StackQueCubre(situacion, bb);
    public TablaDeStack? StackPorClave(string situacion, string claveStack) => _actual.StackPorClave(situacion, claveStack);
    public SpotDeTabla? Spot(string situacion, string claveStack, string claveSpot) => _actual.Spot(situacion, claveStack, claveSpot);
}

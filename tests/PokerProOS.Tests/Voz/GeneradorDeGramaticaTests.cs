using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Voz;
using PokerProOS.Voz.Sapi;

namespace PokerProOS.Tests.Voz;

public class GeneradorDeGramaticaTests
{
    [Fact]
    public void Construir_no_falla_con_un_catalogo_vacio()
    {
        var catalogo = new CatalogoVacio();
        var vocabulario = RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));
        var generador = new GeneradorDeGramatica(catalogo, vocabulario, new OpcionesDeVoz());

        var excepcion = Record.Exception(() => generador.Construir());

        Assert.Null(excepcion);
    }

    private sealed class CatalogoVacio : ICatalogoDeTablas
    {
        public IReadOnlyList<SituacionDeTabla> Situaciones => [];
        public IReadOnlyList<ProblemaDeTabla> Problemas => [];
        public SituacionDeTabla? Situacion(string clave) => null;
        public TablaDeStack? StackQueCubre(string situacion, decimal bb) => null;
        public TablaDeStack? StackPorClave(string situacion, string claveStack) => null;
        public SpotDeTabla? Spot(string situacion, string claveStack, string claveSpot) => null;
    }
}

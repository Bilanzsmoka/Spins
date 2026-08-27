using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class AnalizadorDeMemoriaTests
{
    private static AnalizadorDeMemoria Analizador()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        return new(new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas));
    }

    private static FichaDeMemoria Ficha(
        string mano, string stack = "17-18bb", string spot = "SB_OR",
        string situacion = "HU_SB_OR_FISH")
        => Analizador().Analizar(situacion, stack, spot, mano)!;

    [Fact]
    public void Sin_ficha_cuando_el_spot_no_existe()
        => Assert.Null(Analizador().Analizar("HU_SB_OR_FISH", "17-18bb", "NO_EXISTE", "A8o"));

    [Fact]
    public void Sin_ficha_cuando_la_mano_no_existe()
        => Assert.Null(Analizador().Analizar("HU_SB_OR_FISH", "17-18bb", "SB_OR", "XX"));

    [Fact]
    public void Trae_la_accion_de_la_mano()
    {
        var ficha = Ficha("A8o");
        Assert.Equal("A8o", ficha.Mano);
        Assert.Equal("CALL", ficha.Accion);
        Assert.Equal("17-18bb", ficha.ClaveDeStack);
    }

    [Fact]
    public void El_peso_se_mide_en_combos_de_baraja_no_en_casillas()
    {
        var ficha = Ficha("A8o");
        var raise = ficha.Pesos.Single(p => p.Accion == "RAISE_X2");

        // 84 casillas de 169 son 49,7 %, pero lo que importa es la baraja.
        // 660.0, no 660: la sobrecarga con precisión de xUnit es de double.
        Assert.Equal(660.0, raise.Combos, 3);
        Assert.Equal(49.8, raise.PorcentajeDeBaraja, 1);
    }

    [Fact]
    public void Los_pesos_del_spot_suman_la_baraja_entera()
    {
        var ficha = Ficha("A8o");
        Assert.Equal(100.0, ficha.Pesos.Sum(p => p.PorcentajeDeBaraja), 6);
    }

    [Fact]
    public void Los_pesos_vienen_ordenados_de_mayor_a_menor()
    {
        var pesos = Ficha("A8o").Pesos.Select(p => p.Combos).ToList();
        Assert.Equal(pesos.OrderByDescending(c => c).ToList(), pesos);
    }
}

using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class MixesTests
{
    private static ICatalogoDeTablas Catalogo()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        return new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);
    }

    private static SpotDeTabla SpotConMix() =>
        Catalogo().Spot("HU_BB_OR_FISH", "6-8bb", "BB_VS_SB_LIMP")!;

    [Fact]
    public void Una_mano_mixta_conserva_sus_partes()
    {
        var celda = SpotConMix().CeldaDe("J9o")!;
        Assert.True(celda.EsMixta);
        Assert.Equal(2, celda.Mix!.Count);
        Assert.Equal(100, celda.Mix.Sum(p => p.Frecuencia));
    }

    [Fact]
    public void Una_mano_pura_no_tiene_mix()
    {
        var celda = SpotConMix().CeldaDe("AA")!;
        Assert.False(celda.EsMixta);
        Assert.Null(celda.Mix);
    }

    [Fact]
    public void El_mix_pisa_lo_que_le_hubiera_tocado_por_lista()
    {
        // J9o venia como RAISE_X2_5 puro en la lista explicita; el bloque
        // mixes es la ultima palabra sobre esa mano.
        Assert.True(SpotConMix().CeldaDe("J9o")!.EsMixta);
    }

    [Fact]
    public void La_accion_dominante_es_la_de_mayor_frecuencia()
    {
        var celda = SpotConMix().CeldaDe("J9o")!;
        var mayor = celda.Mix!.MaxBy(p => p.Frecuencia)!.Frecuencia;
        Assert.Equal(mayor, celda.Mix.First(p => p.Accion == celda.Accion).Frecuencia);
    }

    [Fact]
    public void El_spot_sigue_cubriendo_las_169_manos()
        => Assert.Equal(169, SpotConMix().Celdas.Count);

    [Fact]
    public void Una_mano_mixta_se_resuelve_como_borde()
    {
        var resultado = new ResolverManoHandler(Catalogo()).Resolver(
            new ConsultaDeMano("HU_BB_OR_FISH", 7, "BB_VS_SB_LIMP", "J", "9", "o"));

        Assert.Equal("J9o", resultado.Respuesta!.Mano);
        Assert.NotNull(resultado.Respuesta.Mix);
        // Una mano mixta es un borde por definicion: la tabla dice que no hay
        // respuesta unica ahi.
        Assert.True(resultado.Respuesta.EnElBorde);
    }
}

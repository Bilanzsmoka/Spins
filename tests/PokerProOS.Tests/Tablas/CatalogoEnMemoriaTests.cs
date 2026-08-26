using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class CatalogoEnMemoriaTests
{
    private static ICatalogoDeTablas Catalogo() =>
        new CargadorDeTablas(new ValidadorDeTabla(
                RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas);

    [Fact]
    public void Carga_las_once_tablas_sin_problemas()
        => Assert.Empty(Catalogo().Problemas);

    [Fact]
    public void Descubre_la_unica_situacion_existente()
    {
        var situaciones = Catalogo().Situaciones;
        Assert.Single(situaciones);
        Assert.Equal("HU_SB_OR_FISH", situaciones[0].Clave);
    }

    [Fact]
    public void Descubre_los_once_stacks()
        => Assert.Equal(11, Catalogo().Situacion("HU_SB_OR_FISH")!.Stacks.Count);

    [Fact]
    public void Cada_spot_cubre_las_169_manos()
    {
        foreach (var stack in Catalogo().Situacion("HU_SB_OR_FISH")!.Stacks)
            foreach (var spot in stack.Spots)
                Assert.Equal(169, spot.Celdas.Count);
    }

    [Theory]
    [InlineData(7, "7bb")]
    [InlineData(13, "13-16bb")]
    [InlineData(16, "13-16bb")]
    [InlineData(2, "1-4bb")]
    [InlineData(50, "19-99bb")]
    public void Resuelve_el_stack_por_cobertura_y_no_por_texto(decimal bb, string claveEsperada)
        => Assert.Equal(claveEsperada, Catalogo().StackQueCubre("HU_SB_OR_FISH", bb)!.Stack.Clave);

    [Fact]
    public void Devuelve_nulo_para_un_stack_fuera_de_toda_cobertura()
        => Assert.Null(Catalogo().StackQueCubre("HU_SB_OR_FISH", 250));

    [Fact]
    public void Expande_la_accion_marcada_como_resto()
    {
        var spot = Catalogo().Spot("HU_SB_OR_FISH", "10bb", "SB_OR")!;
        Assert.Equal("CALL", spot.AccionDe("AA"));
        Assert.Equal("ALL-IN", spot.AccionDe("A9s"));
        Assert.Equal("CALL", spot.AccionDe("32o"));
    }

    [Fact]
    public void Cuenta_las_manos_por_accion()
    {
        var conteos = Catalogo().Spot("HU_SB_OR_FISH", "10bb", "SB_OR")!.Conteos;
        Assert.Equal(123, conteos["CALL"]);
        Assert.Equal(46, conteos["ALL-IN"]);
        Assert.Equal(169, conteos.Values.Sum());
    }

    [Fact]
    public void Los_stacks_chicos_solo_tienen_tres_spots()
    {
        var catalogo = Catalogo();
        Assert.Equal(3, catalogo.StackPorClave("HU_SB_OR_FISH", "1-4bb")!.Spots.Count);
        Assert.Equal(5, catalogo.StackPorClave("HU_SB_OR_FISH", "6bb")!.Spots.Count);
    }

    [Fact]
    public void Devuelve_nulo_para_un_spot_inexistente_en_ese_stack()
        => Assert.Null(Catalogo().Spot("HU_SB_OR_FISH", "1-4bb", "VS_BB_ISO_3BB"));

    [Fact]
    public void Un_archivo_invalido_no_impide_cargar_los_demas()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"tablas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
        try
        {
            foreach (var archivo in Directory.GetFiles(Rutas.SemillasDeTablas, "*.json"))
                File.Copy(archivo, Path.Combine(directorio, Path.GetFileName(archivo)));
            File.WriteAllText(Path.Combine(directorio, "rota.json"),
                """
                {"situation":{"key":"X","label":"X"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
                   "spots":[{"key":"SB_OR","label":"x","actions":{"LIMP":["AA"],"FOLD":"REST"}}]}]}
                """);

            var catalogo = new CargadorDeTablas(new ValidadorDeTabla(
                    RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
                .CargarDirectorio(directorio);

            Assert.NotEmpty(catalogo.Problemas);
            Assert.All(catalogo.Problemas, p => Assert.Equal("rota.json", p.Archivo));
            Assert.Equal(11, catalogo.Situacion("HU_SB_OR_FISH")!.Stacks.Count);
        }
        finally
        {
            Directory.Delete(directorio, recursive: true);
        }
    }
}

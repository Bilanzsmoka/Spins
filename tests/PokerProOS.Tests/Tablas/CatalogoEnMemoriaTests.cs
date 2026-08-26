using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class CatalogoEnMemoriaTests
{
    private static ICatalogoDeTablas Catalogo()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        return new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);
    }

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

            var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
            var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
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

    /// <summary>
    /// "rota.json" arriba pasa la validación (los campos que le faltan a un
    /// spot como 'label' no los mira ValidadorDeTabla) pero revienta en
    /// LeerArchivo/LeerSpot. Esta prueba fabrica dos archivos así: uno con
    /// 'situation' mal escrito y otro con 'minBB' como texto en vez de
    /// número. Ninguno de los dos puede tumbar la carga de las once tablas
    /// reales, y cada uno debe quedar reportado en Problemas.
    /// </summary>
    [Fact]
    public void Un_archivo_estructuralmente_incompleto_no_impide_cargar_los_demas()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"tablas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
        try
        {
            foreach (var archivo in Directory.GetFiles(Rutas.SemillasDeTablas, "*.json"))
                File.Copy(archivo, Path.Combine(directorio, Path.GetFileName(archivo)));

            File.WriteAllText(Path.Combine(directorio, "sin-situation.json"),
                """
                {"situacion":{"key":"X","label":"X"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
                   "spots":[{"key":"SB_OR","label":"x","actions":{"CALL":["AA"],"FOLD":"REST"}}]}]}
                """);
            File.WriteAllText(Path.Combine(directorio, "minbb-texto.json"),
                """
                {"situation":{"key":"Y","label":"Y"},"stacks":[{"key":"5bb","minBB":"7","maxBB":5,
                   "spots":[{"key":"SB_OR","label":"x","actions":{"CALL":["AA"],"FOLD":"REST"}}]}]}
                """);

            var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
            var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
                .CargarDirectorio(directorio);

            Assert.Equal(11, catalogo.Situacion("HU_SB_OR_FISH")!.Stacks.Count);
            Assert.Contains(catalogo.Problemas, p => p.Archivo == "sin-situation.json");
            Assert.Contains(catalogo.Problemas, p => p.Archivo == "minbb-texto.json");
        }
        finally
        {
            Directory.Delete(directorio, recursive: true);
        }
    }

    /// <summary>
    /// Copiar un archivo de chart para iterar sobre él es un flujo normal;
    /// si el stack no se renombra, dos archivos declaran la misma clave para
    /// la misma situación. Debe quedar reportado como problema y solo una de
    /// las dos copias debe sobrevivir en el catálogo.
    /// </summary>
    [Fact]
    public void Una_clave_de_stack_repetida_entre_archivos_se_reporta_y_no_se_duplica()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"tablas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
        try
        {
            var original = Path.Combine(Rutas.SemillasDeTablas, "hu-sb-or-fish-7bb.json");
            File.Copy(original, Path.Combine(directorio, "hu-sb-or-fish-7bb.json"));
            File.Copy(original, Path.Combine(directorio, "hu-sb-or-fish-7bb-copia.json"));

            var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
            var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
                .CargarDirectorio(directorio);

            Assert.Single(catalogo.Situacion("HU_SB_OR_FISH")!.Stacks);
            Assert.Contains(catalogo.Problemas, p => p.Stack == "7bb");
        }
        finally
        {
            Directory.Delete(directorio, recursive: true);
        }
    }

    /// <summary>
    /// Un chart hand-authored puede escribir la clave de una acción con otra
    /// grafía que la del registro ('call' en vez de 'CALL'); el registro es
    /// case-insensitive así que la validación pasa. El loader debe guardar
    /// la grafía canónica del registro, no la del JSON tal cual, porque el
    /// frontend sí distingue mayúsculas al indexar por clave.
    /// </summary>
    [Fact]
    public void Una_clave_de_accion_con_otra_capitalizacion_resuelve_a_la_grafia_del_registro()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"tablas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
        try
        {
            File.WriteAllText(Path.Combine(directorio, "minusculas.json"),
                """
                {"situation":{"key":"MIN","label":"Minúsculas"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
                   "spots":[{"key":"SB_OR","label":"x","actions":{"call":["AA"],"FOLD":"REST"}}]}]}
                """);

            var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
            var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
                .CargarDirectorio(directorio);

            Assert.Empty(catalogo.Problemas);
            var spot = catalogo.Spot("MIN", "5bb", "SB_OR")!;
            Assert.Equal("CALL", spot.AccionDe("AA"));
        }
        finally
        {
            Directory.Delete(directorio, recursive: true);
        }
    }
}

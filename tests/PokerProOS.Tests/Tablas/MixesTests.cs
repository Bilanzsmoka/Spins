using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

/// <summary>
/// Fabrica su propia tabla en vez de apuntar a una del proyecto: las tablas
/// reales las edita el usuario todo el tiempo, y una prueba que dependa de
/// una mano concreta de una tabla concreta se rompe la primera vez que la
/// corrige.
/// </summary>
public class MixesTests : IDisposable
{
    private readonly string _directorio =
        Path.Combine(Path.GetTempPath(), $"mixes-{Guid.NewGuid():N}");

    public MixesTests()
    {
        Directory.CreateDirectory(_directorio);
        File.WriteAllText(Path.Combine(_directorio, "prueba.json"), """
            {
              "situation": { "key": "PRUEBA", "label": "Prueba" },
              "stacks": [{
                "key": "10bb", "minBB": 10, "maxBB": 10,
                "spots": [{
                  "key": "SB_OR", "label": "Prueba",
                  "actions": {
                    "ALL-IN": ["AA", "KK", "J9o"],
                    "FOLD": "REST"
                  },
                  "mixes": {
                    "J9o": { "ALL-IN": 50, "FOLD": 50 },
                    "QJo": { "ALL-IN": 70, "FOLD": 30 }
                  }
                }]
              }]
            }
            """);
    }

    private ICatalogoDeTablas Catalogo()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        return new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(_directorio);
    }

    private SpotDeTabla Spot() => Catalogo().Spot("PRUEBA", "10bb", "SB_OR")!;

    [Fact]
    public void La_tabla_de_prueba_carga_sin_problemas()
        => Assert.Empty(Catalogo().Problemas);

    [Fact]
    public void Una_mano_mixta_conserva_sus_partes()
    {
        var celda = Spot().CeldaDe("J9o")!;
        Assert.True(celda.EsMixta);
        Assert.Equal(2, celda.Mix!.Count);
        Assert.Equal(100, celda.Mix.Sum(p => p.Frecuencia));
    }

    [Fact]
    public void Una_mano_pura_no_tiene_mix()
    {
        var celda = Spot().CeldaDe("AA")!;
        Assert.False(celda.EsMixta);
        Assert.Null(celda.Mix);
    }

    [Fact]
    public void El_mix_pisa_la_lista_explicita()
    {
        // J9o esta listada como ALL-IN puro arriba; el bloque mixes manda.
        Assert.True(Spot().CeldaDe("J9o")!.EsMixta);
    }

    [Fact]
    public void El_mix_pisa_tambien_al_resto()
    {
        // QJo no esta en ninguna lista: le tocaria FOLD por REST.
        Assert.True(Spot().CeldaDe("QJo")!.EsMixta);
    }

    [Fact]
    public void La_accion_dominante_es_la_de_mayor_frecuencia()
        => Assert.Equal("ALL-IN", Spot().CeldaDe("QJo")!.Accion);

    [Fact]
    public void Un_cincuenta_cincuenta_toma_la_primera_declarada()
        => Assert.Equal("ALL-IN", Spot().CeldaDe("J9o")!.Accion);

    [Fact]
    public void El_spot_sigue_cubriendo_las_169_manos()
        => Assert.Equal(169, Spot().Celdas.Count);

    [Fact]
    public void Una_mano_mixta_se_resuelve_como_borde()
    {
        var resultado = new ResolverManoHandler(Catalogo()).Resolver(
            new ConsultaDeMano("PRUEBA", 10, "SB_OR", "J", "9", "o"));

        Assert.Equal("J9o", resultado.Respuesta!.Mano);
        Assert.NotNull(resultado.Respuesta.Mix);
        // Una mano mixta es un borde por definicion: la tabla dice que ahi no
        // hay respuesta unica.
        Assert.True(resultado.Respuesta.EnElBorde);
    }

    [Fact]
    public void Un_mix_que_no_suma_cien_se_reporta()
    {
        File.WriteAllText(Path.Combine(_directorio, "rota.json"), """
            {
              "situation": { "key": "ROTA", "label": "Rota" },
              "stacks": [{
                "key": "5bb", "minBB": 5, "maxBB": 5,
                "spots": [{
                  "key": "SB_OR", "label": "x",
                  "actions": { "FOLD": "REST" },
                  "mixes": { "AA": { "ALL-IN": 60, "FOLD": 30 } }
                }]
              }]
            }
            """);

        var problemas = Catalogo().Problemas;
        Assert.Contains(problemas, p => p.Mensaje.Contains("90") && p.Mensaje.Contains("100"));
    }

    public void Dispose() => Directory.Delete(_directorio, recursive: true);
}

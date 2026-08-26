using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class ResolverManoTests
{
    private static ResolverManoHandler Handler() => new(
        new CargadorDeTablas(new ValidadorDeTabla(
                RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas));

    private static ConsultaDeMano Consulta(
        decimal bb, string alto, string bajo, string? palo = null, string spot = "SB_OR")
        => new("HU_SB_OR_FISH", bb, spot, alto, bajo, palo);

    [Fact]
    public void Resuelve_una_mano_conocida()
    {
        var resultado = Handler().Resolver(Consulta(10, "A", "9", "s"));
        Assert.Equal("A9s", resultado.Respuesta!.Mano);
        Assert.Equal("ALL-IN", resultado.Respuesta.Accion);
    }

    [Fact]
    public void Asume_offsuit_cuando_no_se_dicta_el_palo()
    {
        var resultado = Handler().Resolver(Consulta(10, "A", "K"));
        Assert.Equal("AKo", resultado.Respuesta!.Mano);
        Assert.True(resultado.Respuesta.PaloAsumido);
    }

    [Fact]
    public void No_marca_palo_asumido_en_una_pareja()
    {
        var resultado = Handler().Resolver(Consulta(10, "A", "A"));
        Assert.Equal("AA", resultado.Respuesta!.Mano);
        Assert.False(resultado.Respuesta.PaloAsumido);
    }

    [Fact]
    public void No_marca_palo_asumido_cuando_se_dicto_el_palo()
        => Assert.False(Handler().Resolver(Consulta(10, "A", "9", "s")).Respuesta!.PaloAsumido);

    [Fact]
    public void Ordena_los_rangos_sin_importar_como_se_dictaron()
    {
        var directo = Handler().Resolver(Consulta(10, "A", "9", "s")).Respuesta!.Mano;
        var invertido = Handler().Resolver(Consulta(10, "9", "A", "s")).Respuesta!.Mano;
        Assert.Equal(directo, invertido);
    }

    [Fact]
    public void Encuentra_el_stack_por_cobertura()
        => Assert.Equal("13-16bb", Handler().Resolver(Consulta(15, "A", "A")).Respuesta!.ClaveDeStack);

    [Fact]
    public void Informa_cuantas_manos_tiene_esa_accion()
    {
        var resultado = Handler().Resolver(Consulta(10, "A", "A"));
        Assert.Equal("CALL", resultado.Respuesta!.Accion);
        Assert.Equal(123, resultado.Respuesta.ManosEnLaAccion);
    }

    [Fact]
    public void Marca_como_borde_una_mano_con_vecina_distinta()
    {
        var spot = SpotDeReferencia();
        var mano = spot.Celdas.First(c =>
            PokerProOS.Domain.Manos.MatrizDeManos.Vecinas(c.Mano)
                .Any(v => spot.AccionDe(v) != c.Accion));
        var partes = Descomponer(mano.Mano);
        var resultado = Handler().Resolver(Consulta(10, partes.Alto, partes.Bajo, partes.Palo));
        Assert.True(resultado.Respuesta!.EnElBorde);
    }

    [Fact]
    public void No_marca_como_borde_una_mano_rodeada_de_la_misma_accion()
    {
        var spot = SpotDeReferencia();
        var mano = spot.Celdas.First(c =>
            PokerProOS.Domain.Manos.MatrizDeManos.Vecinas(c.Mano)
                .All(v => spot.AccionDe(v) == c.Accion));
        var partes = Descomponer(mano.Mano);
        var resultado = Handler().Resolver(Consulta(10, partes.Alto, partes.Bajo, partes.Palo));
        Assert.False(resultado.Respuesta!.EnElBorde);
    }

    [Fact]
    public void Avisa_cuando_el_stack_esta_fuera_de_cobertura()
    {
        var resultado = Handler().Resolver(Consulta(250, "A", "A"));
        Assert.Null(resultado.Respuesta);
        Assert.Equal(MotivoSinRespuesta.StackFueraDeCobertura, resultado.Motivo);
    }

    [Fact]
    public void Avisa_cuando_el_spot_no_existe_en_ese_stack()
    {
        var resultado = Handler().Resolver(Consulta(2, "A", "A", spot: "VS_BB_ISO_3BB"));
        Assert.Null(resultado.Respuesta);
        Assert.Equal(MotivoSinRespuesta.SpotInexistente, resultado.Motivo);
    }

    [Fact]
    public void Avisa_cuando_la_situacion_no_existe()
    {
        var resultado = Handler().Resolver(
            new ConsultaDeMano("NO_EXISTE", 10, "SB_OR", "A", "A", null));
        Assert.Equal(MotivoSinRespuesta.SituacionDesconocida, resultado.Motivo);
    }

    [Fact]
    public void Avisa_cuando_el_rango_no_es_valido()
    {
        var resultado = Handler().Resolver(Consulta(10, "X", "9"));
        Assert.Null(resultado.Respuesta);
        Assert.Equal(MotivoSinRespuesta.ManoInvalida, resultado.Motivo);
    }

    private static SpotDeTabla SpotDeReferencia() =>
        new CargadorDeTablas(new ValidadorDeTabla(
                RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas)
            .Spot("HU_SB_OR_FISH", "10bb", "SB_OR")!;

    private static (string Alto, string Bajo, string? Palo) Descomponer(string mano) =>
        mano.Length == 2
            ? (mano[..1], mano[1..2], null)
            : (mano[..1], mano[1..2], mano[2..3]);
}

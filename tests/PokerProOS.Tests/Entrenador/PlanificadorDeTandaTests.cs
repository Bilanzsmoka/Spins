using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Entrenador;

/// <summary>
/// Catálogo sintético, como ya hace AnalizadorDeMemoriaTests: la regla del
/// planificador se prueba contra una tabla inventada y chica, no contra las
/// del repo, que cambian.
/// </summary>
public class PlanificadorDeTandaTests
{
    /// <summary>
    /// Un spot donde TODO es FOLD salvo AA, que es ALL-IN. Así solo hay un
    /// borde y es fácil de nombrar en las aserciones.
    /// </summary>
    private static SpotDeTabla SpotConUnSoloBorde(string clave) => new(
        clave, $"etiqueta de {clave}",
        MatrizDeManos.Todas()
            .Select(m => new CeldaDeTabla(m, m == "AA" ? "ALL-IN" : "FOLD"))
            .ToList());

    private static ICatalogoDeTablas Catalogo() => new CatalogoEnMemoria(
        [
            new SituacionDeTabla("HU_X", "HU equis | fish", "HU",
            [
                new TablaDeStack(new RangoDeStack("1-5bb", 1, 5), [SpotConUnSoloBorde("SB_OR")]),
                new TablaDeStack(new RangoDeStack("6-9bb", 6, 9), [SpotConUnSoloBorde("SB_OR")]),
            ]),
            new SituacionDeTabla("MAX3_X", "3max equis | fish fish", "3-max",
            [
                new TablaDeStack(new RangoDeStack("1-5bb", 1, 5), [SpotConUnSoloBorde("BTN_OR")]),
            ]),
        ], []);

    private static ProgresoDeCasilla Vencida(string mano, DateOnly vence, string stack = "1-5bb") => new()
    {
        UsuarioId = 1, Situacion = "HU_X", ClaveDeStack = stack, Spot = "SB_OR",
        Mano = mano, AciertosSeguidos = 0, IntervaloEnDias = 1, Vence = vence,
    };

    private static readonly FiltroDeTanda SinFiltro = new(null, null, null, null, null);

    /// <summary>
    /// Lo vencido va primero y lo más vencido antes: si la tanda no alcanza
    /// para todo, lo que más tiempo lleva sin verse es lo que más urge.
    /// </summary>
    [Fact]
    public void Lo_mas_vencido_va_primero()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            [
                Vencida("KK", new DateOnly(2026, 8, 27)),
                Vencida("QQ", new DateOnly(2026, 8, 20)),
                Vencida("JJ", new DateOnly(2026, 8, 25)),
            ],
            yaConocidas: [],
            SinFiltro,
            tamano: 3);

        Assert.Equal(["QQ", "JJ", "KK"], preguntas.Select(p => p.Mano));
        Assert.All(preguntas, p => Assert.False(p.EsNueva));
    }

    /// <summary>
    /// Si lo vencido no llena la tanda, se completa con material nuevo, y ese
    /// material prioriza los bordes: son las casillas que separan saber la
    /// tabla de adivinarla. Acá el único borde del spot es AA.
    /// </summary>
    [Fact]
    public void El_relleno_empieza_por_los_bordes()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            [Vencida("KK", new DateOnly(2026, 8, 27))],
            yaConocidas: [],
            SinFiltro,
            tamano: 2);

        Assert.Equal("KK", preguntas[0].Mano);
        Assert.True(preguntas[1].EsNueva);
        Assert.Equal("AA", preguntas[1].Mano);
    }

    /// <summary>
    /// El relleno no repite lo que ya se estudió: una casilla con progreso que
    /// todavía no vence no es material nuevo.
    /// </summary>
    [Fact]
    public void El_relleno_saltea_lo_que_ya_se_conoce()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            vencidas: [],
            yaConocidas: [ProgresoDeCasilla.Clave("HU_X", "1-5bb", "SB_OR", "AA")],
            SinFiltro,
            tamano: 1);

        Assert.DoesNotContain(preguntas,
            p => p is { Situacion: "HU_X", ClaveDeStack: "1-5bb", Mano: "AA" });
    }

    [Fact]
    public void El_filtro_de_formato_deja_afuera_las_otras_mesas()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            vencidas: [],
            yaConocidas: [],
            new FiltroDeTanda("3-max", null, null, null, null),
            tamano: 20);

        Assert.NotEmpty(preguntas);
        Assert.All(preguntas, p => Assert.Equal("MAX3_X", p.Situacion));
    }

    /// <summary>
    /// El rango de stack se compara contra la cobertura real de cada tabla,
    /// no contra su clave: "6-9bb" entra en un filtro de 7 a 12 porque las dos
    /// bandas se tocan.
    /// </summary>
    [Fact]
    public void El_filtro_de_stack_mira_la_cobertura_de_la_tabla()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            vencidas: [],
            yaConocidas: [],
            new FiltroDeTanda(null, null, 7m, 12m, null),
            tamano: 20);

        Assert.NotEmpty(preguntas);
        Assert.All(preguntas, p => Assert.Equal("6-9bb", p.ClaveDeStack));
    }

    /// <summary>
    /// Una casilla vencida de una tabla que ya no existe se ignora en vez de
    /// romper: las tablas se corrigen a mano y un spot puede desaparecer,
    /// dejando progreso huérfano que no hay que preguntar.
    /// </summary>
    [Fact]
    public void Una_vencida_que_ya_no_existe_en_el_catalogo_se_ignora()
    {
        var huerfana = Vencida("KK", new DateOnly(2026, 8, 1));
        huerfana.Spot = "SPOT_QUE_NO_EXISTE";

        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            [huerfana], yaConocidas: [], SinFiltro, tamano: 5);

        Assert.DoesNotContain(preguntas, p => p.Spot == "SPOT_QUE_NO_EXISTE");
    }

    [Fact]
    public void La_tanda_no_pasa_del_tamano_pedido()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            vencidas: [], yaConocidas: [], SinFiltro, tamano: 4);

        Assert.Equal(4, preguntas.Count);
    }
}

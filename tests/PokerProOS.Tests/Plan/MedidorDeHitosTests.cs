using PokerProOS.Application.Diario;
using PokerProOS.Application.Plan;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Plan;

/// <summary>
/// La única pregunta que el plan tiene que contestar todos los días: ¿voy
/// bien? Estas pruebas fijan qué cuenta como avance, y sobre todo qué NO —que
/// es lo que separa un plan medido de uno que te felicita por nada.
///
/// Catálogo sintético, como el resto de las pruebas del entrenador: la regla
/// se prueba contra una tabla inventada y chica, no contra las del repo, que
/// cambian.
/// </summary>
public class MedidorDeHitosTests
{
    /// <summary>
    /// Un spot donde todo es FOLD salvo una isla ALL-IN en "55". La isla deja
    /// exactamente cinco bordes —ella y sus cuatro vecinas— sobre 169
    /// casillas, así que la diferencia entre contar bordes y contar casillas
    /// es imposible de confundir.
    /// </summary>
    private static SpotDeTabla Spot(string clave) => new(
        clave, $"etiqueta de {clave}",
        MatrizDeManos.Todas()
            .Select(m => new CeldaDeTabla(m, m == "55" ? "ALL-IN" : "FOLD"))
            .ToList());

    private static readonly string[] Bordes = ["55", "65s", "65o", "54s", "54o"];
    private static readonly string[] Stacks = ["1-5bb", "6-9bb", "10-14bb"];

    /// <summary>Tres stacks por cinco bordes: quince, para que 13/15 dé 86,6%.</summary>
    private const int BordesDeLaSituacion = 15;

    private static ICatalogoDeTablas Catalogo() => new CatalogoEnMemoria(
        [
            new SituacionDeTabla("HU_X", "HU equis | fish", "HU",
                Stacks.Select((s, i) => new TablaDeStack(
                    new RangoDeStack(s, i * 5 + 1, i * 5 + 5), [Spot("SB_OR")])).ToList()),
        ], []);

    private sealed class HabitosDePrueba : IRegistroDeHabitos
    {
        public IReadOnlyList<HabitoDefinido> Todos { get; } =
            [new("VOLUMEN", "Volumen", "numero", 1, "", false)];

        public bool Existe(string clave) => clave == "VOLUMEN";
    }

    private static readonly DateOnly Hoy = new(2026, 3, 10);

    private static PlanDefinido Plan(params HitoDefinido[] hitos)
        => new(140, "VOLUMEN", "ESTUDIO", hitos);

    private static HitoDefinido Saber(string situacion = "HU_X", int objetivo = 90) =>
        new("H", "Un hito", "saber", objetivo, Situacion: situacion, EscalonMinimo: 16);

    /// <summary>Las primeras <paramref name="cuantas"/> casillas de borde, sabidas.</summary>
    private static List<ProgresoDeCasilla> Sabidas(int cuantas, int intervalo = 16)
    {
        var casillas = new List<ProgresoDeCasilla>();
        foreach (var stack in Stacks)
            foreach (var mano in Bordes)
            {
                if (casillas.Count == cuantas) return casillas;
                casillas.Add(Casilla(stack, mano, intervalo));
            }
        return casillas;
    }

    private static ProgresoDeCasilla Casilla(string stack, string mano, int intervalo) => new()
    {
        UsuarioId = 1, Situacion = "HU_X", ClaveDeStack = stack, Spot = "SB_OR",
        Mano = mano, AciertosSeguidos = 4, IntervaloEnDias = intervalo, Vence = Hoy,
    };

    private static EstadoDeHito Medir(
        PlanDefinido plan,
        IReadOnlyList<ProgresoDeCasilla> progreso,
        IReadOnlyList<DiaDeGrilla>? dias = null)
        => MedidorDeHitos.Medir(
            plan, Catalogo(), new HabitosDePrueba(), progreso, dias ?? [], Hoy).Hitos[0];

    [Fact]
    public void Con_el_objetivo_alcanzado_el_hito_esta_cumplido()
    {
        var hito = Medir(Plan(Saber()), Sabidas(14));

        Assert.Equal(BordesDeLaSituacion, hito.Total);
        Assert.Equal(14, hito.Hecho);
        Assert.True(hito.Cumplido);
    }

    [Fact]
    public void Por_debajo_del_objetivo_no_esta_cumplido()
    {
        var hito = Medir(Plan(Saber()), Sabidas(13));

        Assert.Equal(86, hito.Porcentaje);
        Assert.False(hito.Cumplido);
    }

    /// <summary>
    /// 13 de 15 es 86,6%. Truncado son 86 y no llega a 87; redondeado sería 87
    /// y el hito se daría por cumplido sin estarlo. Una barra que se completa
    /// sola no sirve para nada.
    /// </summary>
    [Fact]
    public void El_porcentaje_se_trunca_no_se_redondea()
    {
        var hito = Medir(Plan(Saber(objetivo: 87)), Sabidas(13));

        Assert.Equal(86, hito.Porcentaje);
        Assert.False(hito.Cumplido);
    }

    /// <summary>
    /// Contestar no es saber: una casilla con un descanso más corto que el que
    /// pide el hito todavía no demostró nada. Si contara, acertar todo una vez
    /// daría el hito por cumplido el primer día.
    /// </summary>
    [Fact]
    public void Una_casilla_en_un_escalon_menor_no_cuenta()
    {
        var hito = Medir(Plan(Saber()), Sabidas(BordesDeLaSituacion, intervalo: 7));

        Assert.Equal(0, hito.Hecho);
        Assert.False(hito.Cumplido);
    }

    /// <summary>
    /// El denominador son los bordes de la tabla, no sus casillas contestadas.
    /// Si fuera lo segundo, estudiar cinco y acertarlas daría 100% —y no son
    /// las 169 por stack tampoco, que harían el hito cinco veces más largo.
    /// </summary>
    [Fact]
    public void El_denominador_son_los_bordes_no_lo_contestado()
    {
        // "AA" está lejos de la isla: es interior, no borde.
        var interiores = Stacks.Select(s => Casilla(s, "AA", 90)).ToList();

        var hito = Medir(Plan(Saber()), interiores);

        Assert.Equal(BordesDeLaSituacion, hito.Total);
        Assert.Equal(0, hito.Hecho);
    }

    /// <summary>
    /// Un hito que apunta a una tabla que no existe se muestra con su causa y
    /// no frena a los que siguen: es un error del plan, no de la app, y
    /// esconderlo dejaría al usuario esperando un avance que nunca llega.
    /// </summary>
    [Fact]
    public void Un_hito_roto_se_reporta_y_no_frena_a_los_demas()
    {
        var plan = Plan(
            Saber(situacion: "NO_EXISTE") with { Clave = "ROTO" },
            Saber() with { Clave = "BUENO" });

        var estado = MedidorDeHitos.Medir(
            plan, Catalogo(), new HabitosDePrueba(), [], [], Hoy);

        Assert.NotNull(estado.Hitos[0].Problema);
        Assert.False(estado.Hitos[0].EsElActivo);
        Assert.Null(estado.Hitos[1].Problema);
        Assert.True(estado.Hitos[1].EsElActivo);
    }

    [Fact]
    public void El_activo_es_el_primero_sin_cumplir()
    {
        var plan = Plan(
            Saber(objetivo: 1) with { Clave = "YA" },
            Saber(objetivo: 100) with { Clave = "FALTA" });

        var estado = MedidorDeHitos.Medir(
            plan, Catalogo(), new HabitosDePrueba(), Sabidas(14), [], Hoy);

        Assert.True(estado.Hitos[0].Cumplido);
        Assert.False(estado.Hitos[0].EsElActivo);
        Assert.True(estado.Hitos[1].EsElActivo);
    }

    /* ---------- Los hitos de jugar ---------- */

    private static HitoDefinido Jugar(int dias = 5) =>
        new("V", "Volumen", "jugar", 140, Habito: "VOLUMEN", Dias: dias);

    /// <summary>Un día con su volumen. <paramref name="atras"/> cuenta desde hoy.</summary>
    private static DiaDeGrilla Dia(int atras, int volumen) => new(
        Hoy.AddDays(-atras), null,
        new Dictionary<string, int> { ["VOLUMEN"] = volumen },
        new Dictionary<string, string>());

    /// <summary>
    /// Fallar un día no rompe nada. Medir días seguidos hace que la gente
    /// abandone el hábito entero después del primer fallo; la regla que se
    /// sostiene es no fallar dos veces seguidas.
    /// </summary>
    [Fact]
    public void Un_fallo_suelto_no_rompe_el_hito()
    {
        var dias = new List<DiaDeGrilla>
        {
            Dia(4, 150), Dia(3, 0), Dia(2, 150), Dia(1, 150), Dia(0, 150),
        };

        Assert.True(Medir(Plan(Jugar()), [], dias).Cumplido);
    }

    [Fact]
    public void Dos_fallos_seguidos_si_lo_rompen()
    {
        var dias = new List<DiaDeGrilla>
        {
            Dia(4, 150), Dia(3, 0), Dia(2, 0), Dia(1, 150), Dia(0, 150),
        };

        Assert.False(Medir(Plan(Jugar()), [], dias).Cumplido);
    }

    /// <summary>
    /// El día de hoy no puede fallar: todavía no terminó. Sin esto, todas las
    /// mañanas el hito aparecería roto por el solo hecho de no haber jugado
    /// todavía.
    /// </summary>
    [Fact]
    public void El_dia_de_hoy_no_cuenta_como_fallo()
    {
        var dias = new List<DiaDeGrilla>
        {
            Dia(4, 150), Dia(3, 150), Dia(2, 150), Dia(1, 150), Dia(0, 0),
        };

        Assert.True(Medir(Plan(Jugar()), [], dias).Cumplido);
    }

    /// <summary>
    /// Mientras el hito activo sea uno de jugar, igual hay que poder entrenar:
    /// si el botón se apagara dos semanas, el plan dejaría de ser algo que se
    /// hace.
    /// </summary>
    [Fact]
    public void Con_un_hito_de_jugar_activo_igual_toca_una_tabla()
    {
        var estado = MedidorDeHitos.Medir(
            Plan(Jugar(), Saber()), Catalogo(), new HabitosDePrueba(), [], [], Hoy);

        Assert.Equal("jugar", estado.Hitos.Single(h => h.EsElActivo).Tipo);
        Assert.Equal("HU_X", estado.SituacionQueToca);
    }
}

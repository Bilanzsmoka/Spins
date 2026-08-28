using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

public class InterpretadorDeTextoTests
{
    private static InterpretadorDeTexto Armar() =>
        new(RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json")));

    [Theory]
    [InlineData("reina nueve suited", "Q", "9", "s")]
    [InlineData("as rey offsuit", "A", "K", "o")]
    [InlineData("REINA NUEVE SUITED", "Q", "9", "s")]
    public void Interpreta_una_mano(string texto, string alta, string baja, string palo)
    {
        var d = Armar().Interpretar(texto, 0.9f)!;
        Assert.Equal(alta, d.RangoAlto);
        Assert.Equal(baja, d.RangoBajo);
        Assert.Equal(palo, d.Palo);
    }

    [Fact]
    public void Interpreta_stack_y_mano_juntos()
    {
        var d = Armar().Interpretar("nueve be be reina nueve suited", 0.9f)!;
        Assert.Equal(9m, d.StackBB);
        Assert.Equal("Q", d.RangoAlto);
        Assert.Equal("9", d.RangoBajo);
    }

    /// <summary>
    /// El mismo "nueve" es el número del stack y el rango: lo que los separa
    /// es la palabra de stack que va detrás del primero.
    /// </summary>
    [Fact]
    public void El_numero_de_stack_no_se_come_el_rango()
    {
        var d = Armar().Interpretar("quince be be nueve ocho suited", 0.9f)!;
        Assert.Equal(15m, d.StackBB);
        Assert.Equal("9", d.RangoAlto);
        Assert.Equal("8", d.RangoBajo);
    }

    [Theory]
    [InlineData("contra limp", "BB_VS_SB_LIMP")]
    [InlineData("mi accion", "SB_OR")]
    public void Interpreta_un_spot_sin_mano(string texto, string spot)
    {
        var d = Armar().Interpretar(texto, 0.9f)!;
        Assert.Equal(spot, d.Spot);
        Assert.Equal("", d.RangoAlto);
        Assert.Equal("", d.RangoBajo);
    }

    [Fact]
    public void Interpreta_una_situacion_sin_mano()
    {
        var d = Armar().Interpretar("defendiendo limp", 0.9f)!;
        Assert.Equal("HU_BB_VS_LIMP_FISH", d.Situacion);
    }

    [Fact]
    public void Interpreta_un_stack_solo()
    {
        var d = Armar().Interpretar("nueve be be", 0.9f)!;
        Assert.Equal(9m, d.StackBB);
        Assert.Equal("", d.RangoAlto);
    }

    /// <summary>
    /// Lo que la gramática SRGS no podía hacer: negarse. Estaba obligada a
    /// devolver la entrada más parecida, y por eso "cuba" resolvía la reina
    /// y "contra el limite de gastos" cambiaba el spot.
    /// </summary>
    [Theory]
    [InlineData("cuba")]
    [InlineData("contra el limite de gastos")]
    [InlineData("nueve de la noche")]
    [InlineData("dame un momento")]
    [InlineData("")]
    [InlineData("reina nueve suited y despues vemos")]
    public void Rechaza_lo_que_no_es_una_orden(string texto)
        => Assert.Null(Armar().Interpretar(texto, 0.9f));

    [Fact]
    public void Una_mano_sin_palo_deja_el_palo_nulo()
    {
        var d = Armar().Interpretar("as rey", 0.9f)!;
        Assert.Equal("A", d.RangoAlto);
        Assert.Equal("K", d.RangoBajo);
        Assert.Null(d.Palo);
    }

    [Fact]
    public void Conserva_el_texto_crudo_y_la_confianza()
    {
        var d = Armar().Interpretar("as rey offsuit", 0.77f)!;
        Assert.Equal("as rey offsuit", d.TextoCrudo);
        Assert.Equal(0.77f, d.Confianza);
    }
}

using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

/// <summary>
/// Nombrar el nivel al empezar la frase: "spot contra limp", "stack doce",
/// "mano tres dos offsuit".
///
/// El barrido libre mezcla categorías porque no tiene noción de posición ni
/// de flujo: busca todas las formas de todas las categorías en una sola
/// pasada, de la más larga a la más corta. Sobre el vocabulario real eso da
/// 121 choques medidos — "tres max" (formato) se come el "tres" que era el
/// rango, "be be contra limp" (situación) se come el "contra limp" que era el
/// spot. Nombrar el nivel los elimina de raíz: ya no hay dos categorías
/// compitiendo por las mismas palabras.
///
/// La etiqueta es opcional. Sin ella todo se interpreta como antes, así que
/// estas pruebas van de la mano de las de InterpretadorDeTextoTests, que
/// cubren el camino libre.
/// </summary>
public class DictadoDirigidoTests
{
    private static InterpretadorDeTexto Armar() =>
        new(RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json")));

    /// <summary>
    /// El choque que motivó todo: "tres" es el rango 3 y también el arranque
    /// de "tres max", el formato. Sin etiqueta gana el formato porque es la
    /// forma más larga; con "mano" adelante, ni se lo considera.
    /// </summary>
    [Fact]
    public void Una_carta_ya_no_se_va_al_formato()
    {
        var d = Armar().Interpretar("mano tres dos offsuit", 0.9f)!;

        Assert.Equal("3", d.RangoAlto);
        Assert.Equal("2", d.RangoBajo);
        Assert.Equal("o", d.Palo);
        Assert.Null(d.Formato);
    }

    /// <summary>
    /// "contra limp" es un spot y a la vez el final de "be be contra limp",
    /// que es una situación. Pedir el spot tiene que dar el spot y dejar la
    /// situación donde estaba.
    /// </summary>
    [Fact]
    public void Un_spot_pedido_por_su_nombre_no_arrastra_la_situacion()
    {
        var d = Armar().Interpretar("spot contra limp", 0.9f)!;

        Assert.Equal("BB_VS_SB_LIMP", d.Spot);
        Assert.Null(d.Situacion);
        Assert.Null(d.Formato);
    }

    [Fact]
    public void Una_situacion_pedida_por_su_nombre_no_trae_spot()
    {
        var d = Armar().Interpretar("situacion be be contra limp", 0.9f)!;

        Assert.Equal("HU_BB_VS_LIMP_FISH", d.Situacion);
        Assert.Null(d.Spot);
    }

    /// <summary>
    /// Con el nivel dicho, la palabra de stack sobra: lo que distinguía un
    /// número de un rango era ese "be be" detrás, y acá ya lo dijo la
    /// etiqueta.
    /// </summary>
    [Theory]
    [InlineData("stack doce")]
    [InlineData("stack doce be be")]
    [InlineData("fichas doce")]
    public void Un_stack_dirigido_no_necesita_la_palabra_de_stack(string texto)
    {
        var d = Armar().Interpretar(texto, 0.9f)!;

        Assert.Equal(12m, d.StackBB);
        Assert.Equal("", d.RangoAlto);
    }

    [Fact]
    public void Un_formato_pedido_por_su_nombre_no_se_va_a_una_situacion()
    {
        var d = Armar().Interpretar("formato tres max", 0.9f)!;

        Assert.Equal("3-max", d.Formato);
        Assert.Null(d.Situacion);
    }

    /// <summary>
    /// Dicho el nivel, no hay caída al barrido libre. "spot as rey" no es un
    /// spot y adivinar que era una mano sería exactamente lo que la etiqueta
    /// vino a impedir: mejor que no entienda a que salte solo.
    /// </summary>
    [Fact]
    public void Lo_que_no_pertenece_al_nivel_dicho_se_rechaza()
        => Assert.Null(Armar().Interpretar("spot as rey", 0.9f));

    [Fact]
    public void Una_etiqueta_sola_no_es_una_orden()
        => Assert.Null(Armar().Interpretar("spot", 0.9f));

    /// <summary>
    /// Media mano sigue sin alcanzar, aun con la etiqueta: un rango suelto no
    /// resuelve ninguna casilla.
    /// </summary>
    [Fact]
    public void Un_solo_rango_dirigido_se_rechaza()
        => Assert.Null(Armar().Interpretar("mano as", 0.9f));

    /// <summary>
    /// La etiqueta cuenta solo al principio. En el medio es una palabra más
    /// que el vocabulario no explica, y la frase se descarta como cualquier
    /// otra conversación: si contara en cualquier posición, mencionarla al
    /// pasar cambiaría el modo de interpretación sin que nadie lo pidiera.
    /// </summary>
    [Fact]
    public void La_etiqueta_en_el_medio_no_dirige_nada()
        => Assert.Null(Armar().Interpretar("as rey mano", 0.9f));

    /// <summary>
    /// Sin etiqueta todo sigue igual: el dictado corto de siempre no se rompe.
    /// </summary>
    [Fact]
    public void Sin_etiqueta_el_dictado_libre_sigue_andando()
    {
        var d = Armar().Interpretar("reina nueve suited", 0.9f)!;

        Assert.Equal("Q", d.RangoAlto);
        Assert.Equal("9", d.RangoBajo);
        Assert.Equal("s", d.Palo);
    }

    /// <summary>
    /// "mano" encabeza un dictado dirigido y también arranca "mano a mano",
    /// que es una forma del formato heads-up. La frase entera es más larga y
    /// más específica que la etiqueta, así que gana.
    ///
    /// La regla: la etiqueta dirige solo si lo que sigue resuelve en ese
    /// nivel. Si no resuelve, se vuelve al barrido libre con la frase entera.
    /// Eso no reabre la puerta a los saltos —"spot as rey" sigue rechazado,
    /// porque "spot" tampoco significa nada suelto— pero evita que agregar
    /// una etiqueta rompa formas que ya funcionaban.
    /// </summary>
    [Theory]
    [InlineData("mano a mano", "HU")]
    [InlineData("cabeza a cabeza", "HU")]
    public void Una_etiqueta_que_es_parte_de_una_forma_mas_larga_no_dirige(
        string texto, string formato)
    {
        var d = Armar().Interpretar(texto, 0.9f)!;

        Assert.Equal(formato, d.Formato);
    }
}

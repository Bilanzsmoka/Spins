using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

public class RedactorDeRespuestaTests
{
    private static RedactorDeRespuesta Redactor() =>
        new(RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json")),
            RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json")));

    private static ResultadoDeConsulta Con(
        string mano, string accion, int conteo, bool borde, bool asumido) =>
        new(new RespuestaDeMano(mano, accion, conteo, borde, asumido, "7bb"), null, null);

    [Fact]
    public void Dice_solo_la_accion_cuando_no_hay_nada_que_aclarar()
        => Assert.Equal("ALL-IN.", Redactor().Redactar(Con("A9s", "ALL-IN", 113, false, false)));

    [Fact]
    public void No_habla_del_borde_del_rango()
    {
        // "En el borde, N manos" contaba casillas de la grilla y no decía
        // contra qué limita: eso ahora se lee en la ficha, no se escucha.
        var frase = Redactor().Redactar(Con("A9s", "ALL-IN", 113, true, false));

        Assert.Equal("ALL-IN.", frase);
        Assert.DoesNotContain("borde", frase, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Repite_la_mano_cuando_se_asumio_el_palo()
        => Assert.Equal("A K offsuit: CALL.",
            Redactor().Redactar(Con("AKo", "CALL", 43, false, true)));

    [Fact]
    public void Usa_la_etiqueta_del_registro_y_no_la_clave()
        => Assert.Equal("RAISE X2.", Redactor().Redactar(Con("AA", "RAISE_X2", 5, false, false)));

    [Theory]
    [InlineData(MotivoSinRespuesta.StackFueraDeCobertura, "No tengo tabla para 250 be be.")]
    [InlineData(MotivoSinRespuesta.SpotInexistente, "Ese spot no existe a 1-4bb.")]
    public void Repite_el_detalle_cuando_no_hay_respuesta(MotivoSinRespuesta motivo, string detalle)
        => Assert.Equal(detalle, Redactor().Redactar(new ResultadoDeConsulta(null, motivo, detalle)));

    [Fact]
    public void Dice_que_no_entendio_cuando_no_hay_detalle()
        => Assert.Equal("No te entendí.",
            Redactor().Redactar(new ResultadoDeConsulta(null, MotivoSinRespuesta.ManoInvalida, null)));

    [Fact]
    public void La_palabra_del_palo_sale_del_registro_y_no_de_un_literal()
    {
        var vocabularioDeMentira = new RegistroDeVocabularioDeMentira(
            palos: new List<FormasHabladas>
            {
                new("s", new List<string> { "del mismo palo" }),
                new("o", new List<string> { "de palo distinto" }),
            });
        var redactor = new RedactorDeRespuesta(
            RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json")), vocabularioDeMentira);

        Assert.Equal("A K de palo distinto: CALL.",
            redactor.Redactar(Con("AKo", "CALL", 43, false, true)));
    }

    [Fact]
    public void No_repite_la_mano_suited_cuando_no_se_asumio_el_palo()
        => Assert.Equal("CALL.", Redactor().Redactar(Con("AKs", "CALL", 43, false, false)));

    [Fact]
    public void No_repite_la_mano_offsuit_cuando_no_se_asumio_el_palo()
        => Assert.Equal("CALL.", Redactor().Redactar(Con("AKo", "CALL", 43, false, false)));

    [Fact]
    public void Deletrea_un_par_sin_palabra_de_palo()
        => Assert.Equal("A A: CALL.", Redactor().Redactar(Con("AA", "CALL", 43, false, true)));

    private sealed class RegistroDeVocabularioDeMentira(IReadOnlyList<FormasHabladas> palos)
        : IRegistroDeVocabulario
    {
        public IReadOnlyList<string> PalabrasDeStack { get; } = new List<string>();
        public IReadOnlyList<FormasHabladas> Rangos { get; } = new List<FormasHabladas>();
        public IReadOnlyList<FormasHabladas> Palos { get; } = palos;
        public IReadOnlyList<FormasHabladas> Spots { get; } = new List<FormasHabladas>();
        public IReadOnlyList<FormasHabladas> Situaciones { get; } = new List<FormasHabladas>();
        public IReadOnlyList<FormasHabladas> Formatos { get; } = new List<FormasHabladas>();
        public IReadOnlyList<FormasHabladas> Manos { get; } = new List<FormasHabladas>();
    }
}

using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Voz;

public class RedactorDeRespuestaTests
{
    private static RedactorDeRespuesta Redactor() =>
        new(RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json")));

    private static ResultadoDeConsulta Con(
        string mano, string accion, int conteo, bool borde, bool asumido) =>
        new(new RespuestaDeMano(mano, accion, conteo, borde, asumido, "7bb"), null, null);

    [Fact]
    public void Dice_solo_la_accion_cuando_no_hay_nada_que_aclarar()
        => Assert.Equal("ALL-IN.", Redactor().Redactar(Con("A9s", "ALL-IN", 113, false, false)));

    [Fact]
    public void Agrega_el_borde_y_el_conteo_cuando_la_mano_esta_en_el_limite()
        => Assert.Equal("ALL-IN. En el borde, 113 manos.",
            Redactor().Redactar(Con("A9s", "ALL-IN", 113, true, false)));

    [Fact]
    public void Repite_la_mano_cuando_se_asumio_el_palo()
        => Assert.Equal("A K offsuit: CALL.",
            Redactor().Redactar(Con("AKo", "CALL", 43, false, true)));

    [Fact]
    public void Repite_la_mano_y_avisa_del_borde()
        => Assert.Equal("A K offsuit: CALL. En el borde, 43 manos.",
            Redactor().Redactar(Con("AKo", "CALL", 43, true, true)));

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
}

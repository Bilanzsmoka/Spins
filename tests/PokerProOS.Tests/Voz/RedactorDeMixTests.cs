using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Domain.Tablas;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

public class RedactorDeMixTests
{
    private static RedactorDeRespuesta Redactor() => new(
        RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json")),
        RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json")));

    private static ResultadoDeConsulta Con(bool paloAsumido, params ParteDeMix[] partes) =>
        new(new RespuestaDeMano("J9o", partes[0].Accion, 40, true, paloAsumido, "6-8bb", partes),
            null, null);

    [Fact]
    public void Un_cincuenta_cincuenta_se_dice_mitad_y_mitad()
        => Assert.Equal("Mix: mitad RAISE X2.5, mitad CHECK.",
            Redactor().Redactar(Con(false,
                new ParteDeMix("RAISE_X2_5", 50), new ParteDeMix("CHECK", 50))));

    [Fact]
    public void Un_reparto_desparejo_dice_los_porcentajes()
        => Assert.Equal("Mix: 70 por ciento ALL-IN, 30 por ciento FOLD.",
            Redactor().Redactar(Con(false,
                new ParteDeMix("ALL-IN", 70), new ParteDeMix("FOLD", 30))));

    [Fact]
    public void Repite_la_mano_cuando_se_asumio_el_palo()
        => Assert.Equal("J 9 offsuit: mix, mitad RAISE X2.5, mitad CHECK.",
            Redactor().Redactar(Con(true,
                new ParteDeMix("RAISE_X2_5", 50), new ParteDeMix("CHECK", 50))));

    [Fact]
    public void Usa_la_etiqueta_del_registro_y_no_la_clave()
    {
        var frase = Redactor().Redactar(Con(false,
            new ParteDeMix("RAISE_X2_5", 50), new ParteDeMix("CHECK", 50)));
        Assert.Contains("RAISE X2.5", frase);
        Assert.DoesNotContain("RAISE_X2_5", frase);
    }
}

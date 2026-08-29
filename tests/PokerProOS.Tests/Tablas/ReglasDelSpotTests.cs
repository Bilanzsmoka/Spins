using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;

namespace PokerProOS.Tests.Tablas;

/// <summary>
/// La tabla contada en pocas frases.
///
/// Nadie memoriza 169 casillas: memoriza "todos los Ax son all-in" y "los Kx
/// offsuit hasta K7o". Estas pruebas fijan la regla que hace confiable esa
/// traducción — un grupo se nombra sólo si de verdad comprime — porque
/// decir algo falso sobre tablas que estudió otro es peor que no decir nada.
/// </summary>
public class ReglasDelSpotTests
{
    private static SpotDeTabla Spot(Func<string, string> accion) =>
        new("X", "equis", MatrizDeManos.Todas().Select(m => new CeldaDeTabla(m, accion(m))).ToList());

    private static ReglaDelSpot? Buscar(SpotDeTabla spot, string grupo) =>
        ReglasDelSpot.De(spot, 99).FirstOrDefault(r => r.Grupo == grupo);

    [Fact]
    public void Un_grupo_entero_con_la_misma_accion_se_dice_sin_corte()
    {
        // Los Ax offsuit todos ALL-IN; el resto FOLD.
        var spot = Spot(m => m.Length == 3 && m[0] == 'A' && m[2] == 'o' ? "ALL-IN" : "FOLD");

        var regla = Buscar(spot, "los Ax offsuit");

        Assert.NotNull(regla);
        Assert.Equal("ALL-IN", regla.Accion);
        Assert.Null(regla.Hasta);
        Assert.Equal(12, regla.Manos);
    }

    /// <summary>
    /// El corte se dice como fondo —"hasta K7o"— y no como rango completo: el
    /// tope se deduce solo y lo único que hay que recordar es dónde termina.
    /// </summary>
    [Fact]
    public void Un_grupo_que_se_parte_en_dos_dice_hasta_donde_llega()
    {
        // Kx offsuit: ALL-IN de KQo a K7o, CALL de K6o para abajo.
        var altos = new[] { 'Q', 'J', 'T', '9', '8', '7' };
        var spot = Spot(m => m.Length == 3 && m[0] == 'K' && m[2] == 'o'
            ? (altos.Contains(m[1]) ? "ALL-IN" : "CALL")
            : "FOLD");

        var regla = Buscar(spot, "los Kx offsuit");

        Assert.NotNull(regla);
        Assert.Equal("ALL-IN", regla.Accion);
        Assert.Equal("K7o", regla.Hasta);
        Assert.Equal("CALL", regla.Despues);
        Assert.Equal(6, regla.Manos);
    }

    /// <summary>
    /// Si se parte en tres pedazos no se dice nada. Es la regla que evita
    /// enseñar algo falso: "los Qx offsuit son ALL-IN" cuando sólo la mitad
    /// lo son sería peor que no decirlo.
    /// </summary>
    [Fact]
    public void Un_grupo_que_se_parte_en_tres_no_se_nombra()
    {
        // Qx offsuit alterna: ALL-IN, CALL, ALL-IN otra vez.
        var spot = Spot(m =>
        {
            if (m.Length != 3 || m[0] != 'Q' || m[2] != 'o') return "FOLD";
            var i = MatrizDeManos.IndiceDeRango(m[1]);
            return i < 4 ? "ALL-IN" : i < 8 ? "CALL" : "ALL-IN";
        });

        Assert.Null(Buscar(spot, "los Qx offsuit"));
    }

    /// <summary>
    /// Los nombres son los que usa el rubro para memorizar: pares, broadways,
    /// suited connectors. Si el grupo comprime, se dice con su nombre.
    /// </summary>
    [Fact]
    public void Los_grupos_del_rubro_se_nombran_como_los_nombra_el_rubro()
    {
        var spot = Spot(_ => "FOLD");

        var nombres = ReglasDelSpot.De(spot, 99).Select(r => r.Grupo).ToList();

        Assert.Contains("los pares", nombres);
        Assert.Contains("los broadways", nombres);
        Assert.Contains("los suited connectors", nombres);
    }

    /// <summary>
    /// Primero la frase que más tabla explica: con tres renglones en pantalla,
    /// el kilometraje es lo que decide cuál entra.
    /// </summary>
    [Fact]
    public void Se_devuelven_las_que_mas_manos_cubren()
    {
        var spot = Spot(_ => "FOLD");

        var reglas = ReglasDelSpot.De(spot, 3);

        Assert.Equal(3, reglas.Count);
        Assert.True(reglas[0].Manos >= reglas[1].Manos);
        Assert.True(reglas[1].Manos >= reglas[2].Manos);
    }
}

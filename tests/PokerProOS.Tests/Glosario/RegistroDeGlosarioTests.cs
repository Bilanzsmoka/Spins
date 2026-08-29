using PokerProOS.Infrastructure.Glosario;

namespace PokerProOS.Tests.Glosario;

/// <summary>
/// El glosario real, tal como se sirve.
///
/// Los perfiles de jugador no se leen: se reconocen por color y por figura, y
/// existen para etiquetar rivales cuando haya que hacerlo rápido. Eso sólo
/// funciona si el dato está completo y si dos rivales nunca comparten color.
/// </summary>
public class RegistroDeGlosarioTests
{
    private static IReadOnlyList<PokerProOS.Application.Glosario.TerminoDelGlosario> Jugadores()
        => RegistroDeGlosarioJson.Cargar(Rutas.Registro("glosario.json"))
            .Grupos.Single(g => g.Clave == "jugadores").Terminos;

    [Fact]
    public void Cada_perfil_trae_con_que_reconocerlo()
    {
        foreach (var jugador in Jugadores())
        {
            Assert.False(string.IsNullOrWhiteSpace(jugador.Color), jugador.Termino);
            Assert.False(string.IsNullOrWhiteSpace(jugador.ColorTexto), jugador.Termino);
            Assert.False(string.IsNullOrWhiteSpace(jugador.Icono), jugador.Termino);
            Assert.False(string.IsNullOrWhiteSpace(jugador.Perfil), jugador.Termino);
            Assert.False(string.IsNullOrWhiteSpace(jugador.Eje), jugador.Termino);
            Assert.True(jugador.Rasgos?.Count >= 2, jugador.Termino);
        }
    }

    /// <summary>
    /// Un color repetido rompe justamente lo que el color viene a resolver:
    /// mirar la mesa y saber quién es quién sin leer.
    /// </summary>
    [Fact]
    public void Dos_perfiles_nunca_comparten_color()
    {
        var colores = Jugadores().Select(j => j.Color).ToList();

        Assert.Equal(colores.Count, colores.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// La ficha es opcional: una palabra suelta del diccionario no tiene color
    /// ni ícono, y el cargador no puede caerse por eso ni dejar el grupo afuera.
    /// </summary>
    [Fact]
    public void Una_palabra_sin_ficha_se_carga_igual()
    {
        var acciones = RegistroDeGlosarioJson.Cargar(Rutas.Registro("glosario.json"))
            .Grupos.Single(g => g.Clave == "acciones").Terminos;

        Assert.NotEmpty(acciones);
        Assert.All(acciones, a => Assert.False(string.IsNullOrWhiteSpace(a.Explicacion)));
        Assert.All(acciones, a => Assert.Null(a.Color));
    }
}

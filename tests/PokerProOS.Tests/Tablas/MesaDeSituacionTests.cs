using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

/// <summary>
/// La mesa de cada tabla, tal como se dibuja en el entrenador.
///
/// Se controla el archivo y no el código que lo lee: una mesa mal declarada no
/// rompe nada visible —la pantalla dibuja lo que le den— y enseña una mano
/// equivocada durante meses.
/// </summary>
public class MesaDeSituacionTests
{
    private static ICatalogoDeTablas Catalogo()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        return new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);
    }

    [Fact]
    public void Toda_situacion_declara_su_mesa()
        => Assert.All(Catalogo().Situaciones, s =>
            Assert.True(s.Mesa is not null, $"{s.Clave} no declara mesa."));

    /// <summary>
    /// Vos no podés estar sentado en la silla de un rival, y dos rivales no
    /// pueden compartir posición: cualquiera de las dos cosas dibuja una mesa
    /// que no existe.
    /// </summary>
    [Fact]
    public void Nadie_comparte_silla()
    {
        foreach (var situacion in Catalogo().Situaciones)
        {
            var mesa = situacion.Mesa!;
            var sillas = mesa.Rivales.Select(r => r.Posicion).Append(mesa.Heroe).ToList();

            Assert.Equal(
                sillas.Count,
                sillas.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    /// <summary>
    /// El tipo de cada rival tiene que existir en el glosario: de ahí salen su
    /// color y su figura, y un tipo que no está deja la silla gris — que es
    /// justo lo que el color venía a resolver.
    /// </summary>
    [Fact]
    public void El_tipo_de_cada_rival_esta_en_el_glosario()
    {
        var perfiles = PokerProOS.Infrastructure.Glosario.RegistroDeGlosarioJson
            .Cargar(Rutas.Registro("glosario.json"))
            .Grupos.Single(g => g.Clave == "jugadores").Terminos
            .Select(t => t.Termino)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var situacion in Catalogo().Situaciones)
            foreach (var rival in situacion.Mesa!.Rivales)
                Assert.True(
                    perfiles.Contains(rival.Tipo),
                    $"{situacion.Clave}: el tipo «{rival.Tipo}» no está en el glosario.");
    }

    /// <summary>
    /// Un heads-up son dos sillas y un 3-max son tres. Es la cuenta que
    /// convierte "formato" en algo que se puede dibujar.
    /// </summary>
    [Fact]
    public void La_cantidad_de_sillas_coincide_con_el_formato()
    {
        foreach (var situacion in Catalogo().Situaciones)
        {
            var sillas = situacion.Mesa!.Rivales.Count + 1;
            var esperadas = situacion.Formato.Equals("HU", StringComparison.OrdinalIgnoreCase) ? 2 : 3;

            Assert.True(sillas == esperadas,
                $"{situacion.Clave} es {situacion.Formato} y declara {sillas} sillas.");
        }
    }
}

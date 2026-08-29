using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Texto;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Texto;

/// <summary>
/// Los dos intérpretes —el de consultas y el de respuestas— oyen lo mismo.
///
/// Eran dos copias del mismo Normalizar, carácter por carácter. Estas pruebas
/// existen para que agregarle un separador a uno solo no pueda pasar
/// inadvertido: prueban la pieza compartida y, sobre las mismas frases sucias,
/// que los dos intérpretes sigan resolviendo.
/// </summary>
public class NormalizadorDeTextoTests
{
    [Theory]
    [InlineData("Siete BB, a rey.", "siete bb a rey")]
    [InlineData("  ALL   IN;  ", "all in")]
    [InlineData("situación\tcontra limp\n", "situacion contra limp")]
    [InlineData(null, "")]
    public void Baja_tildes_puntuacion_y_espacios(string? texto, string esperado)
        => Assert.Equal(esperado, NormalizadorDeTexto.EnFrase(texto));

    /// <summary>
    /// La forma en palabras y la forma en frase son la misma limpieza: si se
    /// despegaran, un intérprete partiría por donde el otro no.
    /// </summary>
    [Theory]
    [InlineData("As rey OFFSUIT.")]
    [InlineData("doce be be; contra limp")]
    public void Las_dos_formas_parten_igual(string texto)
        => Assert.Equal(
            NormalizadorDeTexto.EnFrase(texto),
            string.Join(' ', NormalizadorDeTexto.EnPalabras(texto)));

    /// <summary>
    /// La misma suciedad —mayúsculas, punto final, espacios de más— pasa por
    /// los dos intérpretes, que ahora normalizan con la misma pieza.
    /// </summary>
    [Fact]
    public void Los_dos_interpretes_toleran_la_misma_suciedad()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var vocabulario = RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));

        Assert.Equal("ALL-IN", new InterpretadorDeRespuesta(acciones).Interpretar("  ALL  IN.  "));

        var dictado = new InterpretadorDeTexto(vocabulario).Interpretar("  AS  REY OFFSUIT.  ", 0.9f)!;
        Assert.Equal("A", dictado.RangoAlto);
        Assert.Equal("K", dictado.RangoBajo);
        Assert.Equal("o", dictado.Palo);
    }
}

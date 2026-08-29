using PokerProOS.Application.Entrenador;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Entrenador;

/// <summary>
/// Entrenando, un dictado es una respuesta y no una consulta. Las formas
/// salen de los `dichos` de acciones.json —las 15 acciones los tienen—, así
/// que agregar una manera de decir "all in" no toca código.
/// </summary>
public class InterpretadorDeRespuestaTests
{
    private static InterpretadorDeRespuesta Armar() =>
        new(RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json")));

    [Theory]
    [InlineData("all in", "ALL-IN")]
    [InlineData("shove", "ALL-IN")]
    [InlineData("ALL IN.", "ALL-IN")]
    public void Reconoce_las_formas_del_registro(string texto, string esperada)
        => Assert.Equal(esperada, Armar().Interpretar(texto));

    /// <summary>
    /// Gana la forma más larga. Sin eso, una acción cuyo dicho sea prefijo de
    /// otra se llevaría las dos, en silencio.
    /// </summary>
    [Fact]
    public void Gana_la_forma_mas_larga()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var interprete = new InterpretadorDeRespuesta(acciones);

        foreach (var accion in acciones.Todas)
            foreach (var dicho in accion.Dichos)
                Assert.Equal(accion.Clave, interprete.Interpretar(dicho));
    }

    /// <summary>
    /// Lo que no es una respuesta no se adivina: entrenando, contestar por
    /// vos una acción que no dijiste te ensucia el calendario.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("pasame la sal")]
    public void Lo_que_no_es_una_accion_devuelve_null(string texto)
        => Assert.Null(Armar().Interpretar(texto));
}

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
    /// Ninguna acción le roba la forma hablada a otra: cada `dicho` de
    /// acciones.json, interpretado, tiene que volver a la clave de la acción
    /// que lo declaró. Si dos acciones compartieran una forma, una se comería
    /// a la otra en silencio —y esta prueba es la que lo detectaría—, aunque
    /// hoy no pasa: `acciones.json` no tiene formas duplicadas.
    /// </summary>
    [Fact]
    public void Cada_forma_declarada_vuelve_a_su_propia_accion()
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

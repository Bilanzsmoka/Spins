using Microsoft.EntityFrameworkCore;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Infrastructure.Database;
using PokerProOS.Infrastructure.Entrenador;

namespace PokerProOS.Tests.Datos;

/// <summary>
/// El mapa de errores: qué se agrupa y qué se descarta.
///
/// Ojo con el alcance: el proveedor en memoria no traduce a SQL, así que estas
/// pruebas fijan la <b>regla</b> y no pueden atrapar un LINQ que SQL Server no
/// sepa ejecutar — eso salió pegándole al endpoint de verdad, y así hay que
/// verificarlo.
/// </summary>
public class BitacoraDeRespuestasSqlTests
{
    private static PokerProOSDbContext ContextoEnMemoria() =>
        new(new DbContextOptionsBuilder<PokerProOSDbContext>()
            .UseInMemoryDatabase($"prueba-{Guid.NewGuid():N}").Options);

    private static RespuestaRegistrada Fila(
        string mano, string elegida, bool acerto, int usuario = 1) => new()
    {
        UsuarioId = usuario, Situacion = "HU_X", ClaveDeStack = "9-11bb", Spot = "SB_OR",
        Mano = mano, AccionElegida = elegida, AccionCorrecta = "ALL-IN",
        Acerto = acerto, Milisegundos = 1200, RespondidaEn = DateTime.UtcNow,
    };

    /// <summary>
    /// Una equivocación suelta es ruido: puede ser un click errado. Lo que se
    /// repite es una regla aprendida al revés, y es lo único que vale mostrar.
    /// </summary>
    [Fact]
    public async Task Un_error_que_paso_una_sola_vez_no_entra()
    {
        using var contexto = ContextoEnMemoria();
        contexto.RespuestasRegistradas.AddRange(
            Fila("AA", "FOLD", acerto: false),
            Fila("KK", "CALL", acerto: false),
            Fila("KK", "CALL", acerto: false));
        await contexto.SaveChangesAsync();

        var errores = await new BitacoraDeRespuestasSql(contexto)
            .ErroresRepetidosAsync(1, 10, CancellationToken.None);

        var solo = Assert.Single(errores);
        Assert.Equal("KK", solo.Mano);
        Assert.Equal(2, solo.Veces);
    }

    /// <summary>
    /// El mismo error, con la misma acción, es un patrón; dos acciones
    /// equivocadas distintas sobre la misma mano son dos cosas distintas y no
    /// se suman: mezclarlas escondería cuál es la que repetís.
    /// </summary>
    [Fact]
    public async Task Se_agrupa_por_la_accion_elegida_y_no_solo_por_la_mano()
    {
        using var contexto = ContextoEnMemoria();
        contexto.RespuestasRegistradas.AddRange(
            Fila("AA", "FOLD", acerto: false), Fila("AA", "FOLD", acerto: false),
            Fila("AA", "CALL", acerto: false), Fila("AA", "CALL", acerto: false),
            Fila("AA", "CALL", acerto: false));
        await contexto.SaveChangesAsync();

        var errores = await new BitacoraDeRespuestasSql(contexto)
            .ErroresRepetidosAsync(1, 10, CancellationToken.None);

        Assert.Equal(2, errores.Count);
        Assert.Equal("CALL", errores[0].AccionElegida);
        Assert.Equal(3, errores[0].Veces);
    }

    [Fact]
    public async Task Los_aciertos_no_son_errores()
    {
        using var contexto = ContextoEnMemoria();
        contexto.RespuestasRegistradas.AddRange(
            Fila("AA", "ALL-IN", acerto: true), Fila("AA", "ALL-IN", acerto: true));
        await contexto.SaveChangesAsync();

        Assert.Empty(await new BitacoraDeRespuestasSql(contexto)
            .ErroresRepetidosAsync(1, 10, CancellationToken.None));
    }

    /// <summary>Los errores son de cada persona, como el progreso.</summary>
    [Fact]
    public async Task No_se_mezclan_los_errores_de_dos_usuarios()
    {
        using var contexto = ContextoEnMemoria();
        contexto.RespuestasRegistradas.AddRange(
            Fila("AA", "FOLD", acerto: false, usuario: 2),
            Fila("AA", "FOLD", acerto: false, usuario: 2));
        await contexto.SaveChangesAsync();

        Assert.Empty(await new BitacoraDeRespuestasSql(contexto)
            .ErroresRepetidosAsync(1, 10, CancellationToken.None));
    }
}

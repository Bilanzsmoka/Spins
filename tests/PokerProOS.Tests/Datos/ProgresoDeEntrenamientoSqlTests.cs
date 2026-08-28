using Microsoft.EntityFrameworkCore;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Infrastructure.Database;
using PokerProOS.Infrastructure.Entrenador;

namespace PokerProOS.Tests.Datos;

public class ProgresoDeEntrenamientoSqlTests
{
    private static PokerProOSDbContext ContextoEnMemoria() =>
        new(new DbContextOptionsBuilder<PokerProOSDbContext>()
            .UseInMemoryDatabase($"prueba-{Guid.NewGuid():N}").Options);

    private static readonly DateOnly Hoy = new(2026, 8, 28);

    private static ProgresoDeCasilla Fila(string mano, DateOnly vence, int usuario = 1) => new()
    {
        UsuarioId = usuario, Situacion = "HU_SB_OR_FISH", ClaveDeStack = "9-11bb",
        Spot = "SB_OR", Mano = mano, AciertosSeguidos = 0, IntervaloEnDias = 1, Vence = vence,
    };

    [Fact]
    public async Task Vencidas_trae_lo_de_hoy_y_lo_de_antes_pero_no_lo_de_manana()
    {
        using var contexto = ContextoEnMemoria();
        contexto.ProgresosDeCasilla.AddRange(
            Fila("AA", Hoy.AddDays(-3)),
            Fila("KK", Hoy),
            Fila("QQ", Hoy.AddDays(1)));
        await contexto.SaveChangesAsync();

        var vencidas = await new ProgresoDeEntrenamientoSql(contexto)
            .VencidasAsync(1, Hoy, CancellationToken.None);

        Assert.Equal(["AA", "KK"], vencidas.Select(v => v.Mano));
    }

    /// <summary>
    /// El progreso es de cada persona: sin filtrar por usuario, la primera que
    /// entrene le arruina la tanda a la siguiente.
    /// </summary>
    [Fact]
    public async Task Vencidas_no_mezcla_usuarios()
    {
        using var contexto = ContextoEnMemoria();
        contexto.ProgresosDeCasilla.AddRange(
            Fila("AA", Hoy, usuario: 1),
            Fila("KK", Hoy, usuario: 2));
        await contexto.SaveChangesAsync();

        var vencidas = await new ProgresoDeEntrenamientoSql(contexto)
            .VencidasAsync(1, Hoy, CancellationToken.None);

        Assert.Equal("AA", Assert.Single(vencidas).Mano);
    }

    [Fact]
    public async Task Guardar_una_fila_nueva_la_inserta()
    {
        using var contexto = ContextoEnMemoria();
        var repositorio = new ProgresoDeEntrenamientoSql(contexto);

        await repositorio.GuardarAsync(Fila("AA", Hoy), CancellationToken.None);

        Assert.Equal(1, await contexto.ProgresosDeCasilla.CountAsync());
    }

    /// <summary>
    /// Volver a contestar la misma casilla actualiza su fila, no agrega otra:
    /// dos filas para una casilla serían dos calendarios que se pisan.
    /// </summary>
    [Fact]
    public async Task Guardar_una_fila_existente_la_actualiza()
    {
        using var contexto = ContextoEnMemoria();
        var repositorio = new ProgresoDeEntrenamientoSql(contexto);
        await repositorio.GuardarAsync(Fila("AA", Hoy), CancellationToken.None);

        var traida = await repositorio.BuscarAsync(
            1, "HU_SB_OR_FISH", "9-11bb", "SB_OR", "AA", CancellationToken.None);
        traida!.AciertosSeguidos = 3;
        traida.Vence = Hoy.AddDays(7);
        await repositorio.GuardarAsync(traida, CancellationToken.None);

        var fila = await contexto.ProgresosDeCasilla.SingleAsync();
        Assert.Equal(3, fila.AciertosSeguidos);
        Assert.Equal(Hoy.AddDays(7), fila.Vence);
    }

    [Fact]
    public async Task Buscar_una_casilla_que_no_existe_devuelve_null()
    {
        using var contexto = ContextoEnMemoria();

        var nada = await new ProgresoDeEntrenamientoSql(contexto).BuscarAsync(
            1, "HU_SB_OR_FISH", "9-11bb", "SB_OR", "AA", CancellationToken.None);

        Assert.Null(nada);
    }
}

using Microsoft.EntityFrameworkCore;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Tests.Datos;

public class ProgresoDeCasillaTests
{
    private static PokerProOSDbContext ContextoEnMemoria() =>
        new(new DbContextOptionsBuilder<PokerProOSDbContext>()
            .UseInMemoryDatabase($"prueba-{Guid.NewGuid():N}").Options);

    private static ProgresoDeCasilla Fila(int usuario = 1, string mano = "AKo") => new()
    {
        UsuarioId = usuario,
        Situacion = "HU_SB_OR_FISH",
        ClaveDeStack = "9-11bb",
        Spot = "SB_OR",
        Mano = mano,
        AciertosSeguidos = 1,
        IntervaloEnDias = 1,
        Vence = new DateOnly(2026, 8, 29),
    };

    [Fact]
    public async Task Guarda_y_relee_una_casilla()
    {
        using var contexto = ContextoEnMemoria();
        contexto.ProgresosDeCasilla.Add(Fila());
        await contexto.SaveChangesAsync();

        var fila = await contexto.ProgresosDeCasilla.SingleAsync();
        Assert.Equal("AKo", fila.Mano);
        Assert.Equal(new DateOnly(2026, 8, 29), fila.Vence);
    }

    /// <summary>
    /// La misma casilla de dos usuarios son dos filas. Es la razón por la que
    /// UsuarioId va en la clave desde el día uno: el día que haya login, el
    /// progreso ya está separado y no hay que migrar nada.
    /// </summary>
    [Fact]
    public async Task La_misma_casilla_de_dos_usuarios_son_dos_filas()
    {
        using var contexto = ContextoEnMemoria();
        contexto.ProgresosDeCasilla.AddRange(Fila(usuario: 1), Fila(usuario: 2));
        await contexto.SaveChangesAsync();

        Assert.Equal(2, await contexto.ProgresosDeCasilla.CountAsync());
    }

    /// <summary>
    /// La clave compuesta se arma en un solo lugar: el planificador la usa
    /// para saber qué casillas ya conoce, y dos formas distintas de armarla
    /// harían que material ya visto reapareciera como nuevo.
    /// </summary>
    [Fact]
    public void La_clave_compuesta_junta_los_cuatro_campos()
        => Assert.Equal(
            "HU_SB_OR_FISH|9-11bb|SB_OR|AKo",
            ProgresoDeCasilla.Clave("HU_SB_OR_FISH", "9-11bb", "SB_OR", "AKo"));
}

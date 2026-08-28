using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Tests.Datos;

public class BitacoraDeConsultasTests
{
    private static PokerProOSDbContext ContextoEnMemoria() =>
        new(new DbContextOptionsBuilder<PokerProOSDbContext>()
            .UseInMemoryDatabase($"prueba-{Guid.NewGuid():N}").Options);

    // Caso real, no de laboratorio: palo asumido (repite la mano deletreada)
    // mas en el borde (agrega el conteo de manos). Esa combinacion es la que
    // superaba los 20 caracteres que el campo Accion tenia reservados cuando
    // ahi se guardaba la frase entera en lugar del codigo de accion.
    private static readonly EventoDeCopiloto EventoRealista = new(
        TextoCrudo: "a k suited raise",
        ManoInterpretada: "AKs",
        Accion: "RAISE_X2",
        Respuesta: "A K suited: RAISE X2. En el borde, 113 manos.",
        Resuelta: true,
        Tipo: TipoDeDictado.Mano,
        Situacion: "HU_SB_OR_FISH",
        ClaveDeStack: "7bb",
        Spot: "SB_OR");

    [Fact]
    public async Task Registra_la_frase_completa_y_el_codigo_de_accion_por_separado()
    {
        using var contexto = ContextoEnMemoria();
        var bitacora = new BitacoraDeConsultas(contexto, NullLogger<BitacoraDeConsultas>.Instance);

        await bitacora.RegistrarAsync(EventoRealista, CancellationToken.None);

        var filas = await contexto.ConsultasDeVoz.ToListAsync(CancellationToken.None);
        var fila = Assert.Single(filas);
        Assert.Equal("RAISE_X2", fila.Accion);
        Assert.Equal("A K suited: RAISE X2. En el borde, 113 manos.", fila.Respuesta);
    }

    [Fact]
    public async Task RegistrarAsync_nunca_lanza_aunque_falle_el_guardado()
    {
        using var contexto = new ContextoQueFalla(new DbContextOptionsBuilder<PokerProOSDbContext>()
            .UseInMemoryDatabase($"prueba-{Guid.NewGuid():N}").Options);
        var bitacora = new BitacoraDeConsultas(contexto, NullLogger<BitacoraDeConsultas>.Instance);

        // Si RegistrarAsync propagara la falla, la excepcion escaparia aca
        // y la prueba se veria como fallida por excepcion, no por un Assert:
        // esa es exactamente la garantia que esta clase existe para dar.
        await bitacora.RegistrarAsync(EventoRealista, CancellationToken.None);
    }

    /// <summary>
    /// Contexto de prueba cuyo <see cref="SaveChangesAsync(CancellationToken)"/>
    /// siempre falla, para simular una base que rechaza el guardado (fuera de
    /// servicio, columna que no acepta el valor, lo que sea) sin depender de
    /// una base real.
    /// </summary>
    private sealed class ContextoQueFalla(DbContextOptions<PokerProOSDbContext> opciones)
        : PokerProOSDbContext(opciones)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("fallo simulado de guardado");
    }
}

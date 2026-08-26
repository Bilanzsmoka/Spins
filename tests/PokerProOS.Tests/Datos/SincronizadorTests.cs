using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Database;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Datos;

public class SincronizadorTests
{
    private static PokerProOSDbContext ContextoEnMemoria() =>
        new(new DbContextOptionsBuilder<PokerProOSDbContext>()
            .UseInMemoryDatabase($"prueba-{Guid.NewGuid():N}").Options);

    private static ICatalogoDeTablas Catalogo()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        return new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);
    }

    [Fact]
    public async Task Sincroniza_todas_las_celdas_del_catalogo()
    {
        using var contexto = ContextoEnMemoria();
        var catalogo = Catalogo();
        var escritas = await new SincronizadorDeCatalogo(contexto)
            .SincronizarAsync(catalogo, CancellationToken.None);

        // El total se deriva del catalogo, no se fija: cada tabla nueva lo cambia.
        var esperadas = catalogo.Situaciones
            .SelectMany(s => s.Stacks)
            .SelectMany(t => t.Spots)
            .Count() * 169;
        Assert.Equal(esperadas, escritas);
        Assert.Equal(escritas, await contexto.ChartStrategyCells.CountAsync(
            CancellationToken.None));
    }

    [Fact]
    public async Task Sincronizar_dos_veces_no_duplica_filas()
    {
        using var contexto = ContextoEnMemoria();
        var sincronizador = new SincronizadorDeCatalogo(contexto);
        var catalogo = Catalogo();

        await sincronizador.SincronizarAsync(catalogo, CancellationToken.None);
        var primera = await contexto.ChartStrategyCells.CountAsync(CancellationToken.None);
        await sincronizador.SincronizarAsync(catalogo, CancellationToken.None);
        var segunda = await contexto.ChartStrategyCells.CountAsync(CancellationToken.None);

        Assert.Equal(primera, segunda);
    }
}

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

    private static ICatalogoDeTablas Catalogo() =>
        new CargadorDeTablas(new ValidadorDeTabla(
                RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas);

    [Fact]
    public async Task Sincroniza_todas_las_celdas_del_catalogo()
    {
        using var contexto = ContextoEnMemoria();
        var escritas = await new SincronizadorDeCatalogo(contexto)
            .SincronizarAsync(Catalogo(), CancellationToken.None);

        // 11 stacks: dos con 3 spots y nueve con 5, por 169 manos.
        Assert.Equal((2 * 3 + 9 * 5) * 169, escritas);
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

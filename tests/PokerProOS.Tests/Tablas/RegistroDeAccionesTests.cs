using PokerProOS.Infrastructure;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class RegistroDeAccionesTests : IDisposable
{
    private static string Ruta => Rutas.Registro("acciones.json");
    private readonly List<string> _temporales = [];

    [Fact]
    public void Carga_las_cuatro_acciones_del_proyecto()
        => Assert.Equal(4, RegistroDeAccionesJson.Cargar(Ruta).Todas.Count);

    [Theory]
    [InlineData("ALL-IN", "#43bf55")]
    [InlineData("CALL", "#ffb743")]
    [InlineData("RAISE_X2", "#7c86dc")]
    [InlineData("FOLD", "#edf3fb")]
    public void Conserva_los_colores_del_proyecto_original(string clave, string color)
        => Assert.Equal(color, RegistroDeAccionesJson.Cargar(Ruta).Obtener(clave).Color);

    [Fact]
    public void Ordena_las_acciones_para_la_leyenda()
    {
        var claves = RegistroDeAccionesJson.Cargar(Ruta).Todas.Select(a => a.Clave);
        Assert.Equal(["ALL-IN", "CALL", "RAISE_X2", "FOLD"], claves);
    }

    [Fact]
    public void Reconoce_una_accion_existente()
        => Assert.True(RegistroDeAccionesJson.Cargar(Ruta).Existe("FOLD"));

    [Fact]
    public void Rechaza_una_accion_inexistente()
        => Assert.False(RegistroDeAccionesJson.Cargar(Ruta).Existe("LIMP"));

    [Fact]
    public void Falla_al_pedir_una_accion_inexistente()
        => Assert.Throws<KeyNotFoundException>(
            () => RegistroDeAccionesJson.Cargar(Ruta).Obtener("LIMP"));

    [Fact]
    public void Cada_accion_declara_al_menos_una_forma_hablada()
        => Assert.All(RegistroDeAccionesJson.Cargar(Ruta).Todas,
            a => Assert.NotEmpty(a.Dichos));

    [Fact]
    public void Falla_con_un_mensaje_legible_ante_un_archivo_malformado()
    {
        var ruta = Fabricar("""{"acciones": [ esto no es json valido""");

        var excepcion = Assert.Throws<RegistroInvalidoException>(
            () => RegistroDeAccionesJson.Cargar(ruta));

        Assert.Contains(ruta, excepcion.Message);
        Assert.Equal(ruta, excepcion.RutaArchivo);
    }

    private string Fabricar(string contenido)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"acciones-{Guid.NewGuid():N}.json");
        File.WriteAllText(ruta, contenido);
        _temporales.Add(ruta);
        return ruta;
    }

    public void Dispose()
    {
        foreach (var ruta in _temporales) File.Delete(ruta);
    }
}

using PokerProOS.Infrastructure;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class RegistroDeAccionesTests : IDisposable
{
    private static string Ruta => Rutas.Registro("acciones.json");
    private readonly List<string> _temporales = [];

    // No se fija un numero: el registro crece cada vez que una tabla nueva
    // trae una accion que no existia, y una prueba que cuente romperia con
    // cada tabla agregada, que es exactamente lo que el proyecto busca que
    // sea barato.
    [Fact]
    public void Carga_todas_las_acciones_declaradas_en_el_archivo()
    {
        var declaradas = System.Text.Json.JsonDocument
            .Parse(File.ReadAllText(Ruta)).RootElement
            .GetProperty("acciones").GetArrayLength();
        Assert.Equal(declaradas, RegistroDeAccionesJson.Cargar(Ruta).Todas.Count);
    }

    [Fact]
    public void No_repite_ninguna_clave()
    {
        var claves = RegistroDeAccionesJson.Cargar(Ruta).Todas.Select(a => a.Clave).ToList();
        Assert.Equal(claves.Count, claves.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("ALL-IN", "#43bf55")]
    [InlineData("CALL", "#ffb743")]
    [InlineData("RAISE_X2", "#7c86dc")]
    [InlineData("FOLD", "#edf3fb")]
    public void Conserva_los_colores_del_proyecto_original(string clave, string color)
        => Assert.Equal(color, RegistroDeAccionesJson.Cargar(Ruta).Obtener(clave).Color);

    [Fact]
    public void Ordena_las_acciones_por_su_orden_declarado()
    {
        var ordenes = RegistroDeAccionesJson.Cargar(Ruta).Todas.Select(a => a.Orden).ToList();
        Assert.Equal(ordenes.OrderBy(o => o), ordenes);
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

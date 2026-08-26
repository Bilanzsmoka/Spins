using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

public class RegistroDeVocabularioTests : IDisposable
{
    private static IRegistroDeVocabulario Cargar() =>
        RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));

    private readonly List<string> _temporales = [];

    [Fact]
    public void Declara_los_trece_rangos()
        => Assert.Equal(13, Cargar().Rangos.Count);

    [Fact]
    public void Declara_los_dos_palos()
        => Assert.Equal(2, Cargar().Palos.Count);

    [Fact]
    public void Cada_rango_tiene_al_menos_una_forma_hablada()
        => Assert.All(Cargar().Rangos, r => Assert.NotEmpty(r.Dichos));

    [Fact]
    public void Las_claves_de_rango_coinciden_con_la_matriz()
    {
        var delVocabulario = Cargar().Rangos.Select(r => r.Clave[0]).OrderBy(c => c);
        var deLaMatriz = PokerProOS.Domain.Manos.MatrizDeManos.Rangos.OrderBy(c => c);
        Assert.Equal(deLaMatriz, delVocabulario);
    }

    [Fact]
    public void Ninguna_forma_hablada_se_repite_entre_rangos()
    {
        var todos = Cargar().Rangos.SelectMany(r => r.Dichos).ToList();
        Assert.Equal(todos.Count, todos.Distinct().Count());
    }

    [Fact]
    public void Los_spots_declarados_existen_en_las_tablas()
    {
        var deLasTablas = SpotsDeLasTablas();
        Assert.All(Cargar().Spots, s => Assert.Contains(s.Clave, deLasTablas));
    }

    [Fact]
    public void Todo_spot_de_las_tablas_tiene_forma_hablada()
    {
        var deLasTablas = SpotsDeLasTablas();
        var delVocabulario = Cargar().Spots.Select(s => s.Clave).ToHashSet();

        Assert.All(deLasTablas, clave => Assert.Contains(clave, delVocabulario));
    }

    [Fact]
    public void Toda_situacion_de_las_tablas_tiene_forma_hablada()
    {
        var catalogo = CargarCatalogo();
        var deLasTablas = catalogo.Situaciones.Select(s => s.Clave).Distinct();
        var delVocabulario = Cargar().Situaciones.Select(s => s.Clave).ToHashSet();

        Assert.All(deLasTablas, clave => Assert.Contains(clave, delVocabulario));
    }

    private static PokerProOS.Application.Tablas.ICatalogoDeTablas CargarCatalogo() =>
        new PokerProOS.Infrastructure.Tablas.CargadorDeTablas(
                new PokerProOS.Infrastructure.Tablas.ValidadorDeTabla(
                    PokerProOS.Infrastructure.Tablas.RegistroDeAccionesJson.Cargar(
                        Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas);

    private static HashSet<string> SpotsDeLasTablas() =>
        CargarCatalogo().Situaciones
            .SelectMany(s => s.Stacks).SelectMany(t => t.Spots)
            .Select(s => s.Clave).Distinct().ToHashSet();

    [Fact]
    public void Falla_con_un_mensaje_legible_ante_un_archivo_malformado()
    {
        var ruta = Fabricar("""{"palabrasDeStack": [ esto no es json valido""");

        var excepcion = Assert.Throws<RegistroInvalidoException>(
            () => RegistroDeVocabularioJson.Cargar(ruta));

        Assert.Contains(ruta, excepcion.Message);
        Assert.Equal(ruta, excepcion.RutaArchivo);
    }

    private string Fabricar(string contenido)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"vocabulario-{Guid.NewGuid():N}.json");
        File.WriteAllText(ruta, contenido);
        _temporales.Add(ruta);
        return ruta;
    }

    public void Dispose()
    {
        foreach (var ruta in _temporales) File.Delete(ruta);
    }
}

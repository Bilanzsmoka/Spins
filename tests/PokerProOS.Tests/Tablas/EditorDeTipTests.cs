using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class EditorDeTipTests : IDisposable
{
    private readonly string _directorio =
        Path.Combine(Path.GetTempPath(), "tips-" + Guid.NewGuid().ToString("N"));

    private readonly EditorDeTablasJson _editor;
    private readonly CatalogoVivo _catalogo;

    public EditorDeTipTests()
    {
        Directory.CreateDirectory(_directorio);
        foreach (var archivo in Directory.GetFiles(Rutas.SemillasDeTablas, "*.json"))
            File.Copy(archivo, Path.Combine(_directorio, Path.GetFileName(archivo)));

        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var cargador = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones);
        _catalogo = new CatalogoVivo(cargador.CargarDirectorio(_directorio));
        _editor = new EditorDeTablasJson(_directorio, _catalogo, cargador);
    }

    public void Dispose()
    {
        Directory.Delete(_directorio, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Guarda_el_tip_y_recarga_el_catalogo()
    {
        var resultado = await _editor.EditarTipAsync(new EdicionDeTip(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "Los ases bajos suben por el color."), default);

        Assert.True(resultado.Exito);
        Assert.Equal(
            "Los ases bajos suben por el color.",
            _catalogo.Spot("HU_SB_OR_FISH", "17-18bb", "SB_OR")!.Tip);
    }

    [Fact]
    public async Task Un_texto_vacio_borra_el_tip()
    {
        await _editor.EditarTipAsync(new EdicionDeTip(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "algo"), default);
        var resultado = await _editor.EditarTipAsync(new EdicionDeTip(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "   "), default);

        Assert.True(resultado.Exito);
        Assert.Null(_catalogo.Spot("HU_SB_OR_FISH", "17-18bb", "SB_OR")!.Tip);
        // Y la clave no queda vacía en el archivo, que sería un ProblemaDeTabla.
        Assert.Empty(resultado.Problemas);
    }

    [Fact]
    public async Task Avisa_cuando_el_spot_no_existe()
    {
        var resultado = await _editor.EditarTipAsync(new EdicionDeTip(
            "HU_SB_OR_FISH", "17-18bb", "NO_EXISTE", "x"), default);

        Assert.False(resultado.Exito);
        Assert.NotNull(resultado.Error);
    }
}

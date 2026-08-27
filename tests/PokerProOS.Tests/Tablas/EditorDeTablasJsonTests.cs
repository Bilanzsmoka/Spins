using System.Text.Json.Nodes;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class EditorDeTablasJsonTests : IDisposable
{
    private readonly string _directorio =
        Path.Combine(Path.GetTempPath(), "tips-" + Guid.NewGuid().ToString("N"));

    private readonly EditorDeTablasJson _editor;
    private readonly CatalogoVivo _catalogo;

    public EditorDeTablasJsonTests()
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

    // --- Edición de celda (EditarAsync): los dos refactors de esta tarea
    // (UbicarSpot con firma nueva, GuardarYRecargar extraído) tocan este
    // camino, y no había ningún test que lo ejerciera. Estos cuatro cubren
    // ese hueco. Los valores concretos (mano, acción de partida) están
    // tomados de lo que hoy dice hu-sb-or-fish-17-18bb.json: en el spot
    // SB_OR, "AA" y "KQo" no están en ninguna lista explícita (CALL,
    // ALL-IN), así que hoy les toca RAISE_X2 por ser el REST del spot.

    [Fact]
    public async Task Una_accion_pura_reemplaza_la_que_tenia()
    {
        Assert.Equal("RAISE_X2", _catalogo.Spot("HU_SB_OR_FISH", "17-18bb", "SB_OR")!.AccionDe("AA"));

        var resultado = await _editor.EditarAsync(new EdicionDeCelda(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "AA", "CALL", null), default);

        Assert.True(resultado.Exito);
        Assert.Equal("CALL", _catalogo.Spot("HU_SB_OR_FISH", "17-18bb", "SB_OR")!.AccionDe("AA"));
    }

    [Fact]
    public async Task Un_mix_deja_la_celda_mixta_con_esas_partes()
    {
        var resultado = await _editor.EditarAsync(new EdicionDeCelda(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "KQo", null,
            [new ParteDeMix("CALL", 50), new ParteDeMix("RAISE_X2", 50)]), default);

        Assert.True(resultado.Exito);
        var celda = _catalogo.Spot("HU_SB_OR_FISH", "17-18bb", "SB_OR")!.CeldaDe("KQo")!;
        Assert.True(celda.EsMixta);
        Assert.Equal(2, celda.Mix!.Count);
        Assert.Contains(celda.Mix, p => p.Accion == "CALL" && p.Frecuencia == 50);
        Assert.Contains(celda.Mix, p => p.Accion == "RAISE_X2" && p.Frecuencia == 50);
    }

    [Fact]
    public async Task Pasar_de_mix_a_accion_pura_no_deja_rastro_en_mixes()
    {
        await _editor.EditarAsync(new EdicionDeCelda(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "KQo", null,
            [new ParteDeMix("CALL", 50), new ParteDeMix("RAISE_X2", 50)]), default);
        Assert.True(_catalogo.Spot("HU_SB_OR_FISH", "17-18bb", "SB_OR")!.CeldaDe("KQo")!.EsMixta);

        var resultado = await _editor.EditarAsync(new EdicionDeCelda(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "KQo", "ALL-IN", null), default);

        Assert.True(resultado.Exito);
        var celda = _catalogo.Spot("HU_SB_OR_FISH", "17-18bb", "SB_OR")!.CeldaDe("KQo")!;
        Assert.False(celda.EsMixta);
        Assert.Null(celda.Mix);
        Assert.Equal("ALL-IN", celda.Accion);

        // No alcanza con lo que dice el catálogo recargado: hay que confirmar
        // que Aplicar sacó la mano del bloque "mixes" en el archivo mismo,
        // no solo que el catálogo la resuelve distinto.
        var archivo = Path.Combine(_directorio, "hu-sb-or-fish-17-18bb.json");
        var raiz = JsonNode.Parse(await File.ReadAllTextAsync(archivo))!.AsObject();
        var spot = raiz["stacks"]!.AsArray()
            .Select(n => n!.AsObject())
            .First(s => s["key"]!.GetValue<string>() == "17-18bb")
            ["spots"]!.AsArray()
            .Select(n => n!.AsObject())
            .First(s => s["key"]!.GetValue<string>() == "SB_OR");
        if (spot["mixes"] is JsonObject mixes)
            Assert.DoesNotContain("KQo", mixes.Select(kv => kv.Key));
    }

    [Fact]
    public async Task Despues_de_editar_siguen_cubiertas_las_169_manos_sin_problemas_nuevos()
    {
        var resultado = await _editor.EditarAsync(new EdicionDeCelda(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "AA", "CALL", null), default);

        Assert.True(resultado.Exito);
        Assert.Empty(resultado.Problemas);
        Assert.Equal(169, _catalogo.Spot("HU_SB_OR_FISH", "17-18bb", "SB_OR")!.Celdas.Count);
    }
}

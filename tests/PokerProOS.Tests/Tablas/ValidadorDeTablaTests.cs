using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class ValidadorDeTablaTests : IDisposable
{
    private readonly ValidadorDeTabla _validador =
        new(RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json")));
    private readonly List<string> _temporales = [];

    [Fact]
    public void Todas_las_tablas_reales_del_proyecto_son_validas()
    {
        var archivos = Directory.GetFiles(Rutas.SemillasDeTablas, "*.json");
        // Que haya tablas, no cuantas: el usuario agrega archivos seguido.
        Assert.NotEmpty(archivos);
        foreach (var archivo in archivos)
        {
            var resultado = _validador.Validar(archivo);
            Assert.True(resultado.EsValido,
                $"{Path.GetFileName(archivo)}: " +
                string.Join(" | ", resultado.Problemas.Select(p => p.Mensaje)));
        }
    }

    [Fact]
    public void Detecta_una_mano_repetida_entre_dos_acciones()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{
            "CALL":["AA","KK"],"ALL-IN":["AA"],"FOLD":"REST"}}]}]}
            """);
        var problemas = _validador.Validar(ruta).Problemas;
        Assert.Contains(problemas, p => p.Mensaje.Contains("AA") && p.Mensaje.Contains("duplicada"));
    }

    [Fact]
    public void Detecta_una_accion_fuera_del_registro()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{
            "LIMP":["AA"],"FOLD":"REST"}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas,
            p => p.Mensaje.Contains("LIMP") && p.Mensaje.Contains("registro"));
    }

    [Fact]
    public void Detecta_dos_acciones_marcadas_como_resto()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{
            "CALL":"REST","FOLD":"REST"}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas, p => p.Mensaje.Contains("REST"));
    }

    [Fact]
    public void Detecta_cobertura_incompleta_sin_resto()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{
            "CALL":["AA","KK"],"FOLD":["QQ"]}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas,
            p => p.Mensaje.Contains("169") && p.Mensaje.Contains("3"));
    }

    [Fact]
    public void Detecta_una_etiqueta_de_mano_inexistente()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{
            "CALL":["XZ9"],"FOLD":"REST"}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas, p => p.Mensaje.Contains("XZ9"));
    }

    [Fact]
    public void Detecta_un_conteo_declarado_que_no_cuadra()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{"CALL":["AA"],"FOLD":"REST"},
            "expectedCounts":{"CALL":99,"FOLD":70,"TOTAL":169}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas,
            p => p.Mensaje.Contains("CALL") && p.Mensaje.Contains("99"));
    }

    [Fact]
    public void Detecta_un_check_declarado_que_no_cuadra()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{"CALL":["AA"],"FOLD":"REST"},
            "checks":{"AA":"FOLD"}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas, p => p.Mensaje.Contains("AA"));
    }

    [Fact]
    public void Informa_archivo_stack_y_spot_del_problema()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"9bb","minBB":9,"maxBB":9,
            "spots":[{"key":"VS_BB_3BET","label":"x","actions":{"LIMP":["AA"],"FOLD":"REST"}}]}]}
            """);
        var problema = Assert.Single(_validador.Validar(ruta).Problemas);
        Assert.Equal("9bb", problema.Stack);
        Assert.Equal("VS_BB_3BET", problema.Spot);
        Assert.Equal(Path.GetFileName(ruta), problema.Archivo);
    }

    [Fact]
    public void No_lanza_y_reporta_un_stack_sin_clave()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{"FOLD":"REST"}}]}]}
            """);
        var resultado = _validador.Validar(ruta);
        Assert.NotEmpty(resultado.Problemas);
    }

    [Fact]
    public void No_lanza_y_reporta_un_spot_sin_clave()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"label":"SB OR","actions":{"FOLD":"REST"}}]}]}
            """);
        var resultado = _validador.Validar(ruta);
        Assert.NotEmpty(resultado.Problemas);
    }

    [Fact]
    public void No_lanza_y_reporta_un_conteo_declarado_que_no_es_un_entero()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{"CALL":["AA"],"FOLD":"REST"},
            "expectedCounts":{"CALL":"muchas","FOLD":168,"TOTAL":169}}]}]}
            """);
        var resultado = _validador.Validar(ruta);
        Assert.NotEmpty(resultado.Problemas);
    }

    [Fact]
    public void No_lanza_y_reporta_contenido_que_no_es_json()
    {
        var ruta = Fabricar("esto no es json en absoluto");
        var resultado = _validador.Validar(ruta);
        Assert.NotEmpty(resultado.Problemas);
    }

    [Fact]
    public void No_lanza_y_reporta_un_archivo_sin_stacks()
    {
        var ruta = Fabricar("""{"situation":{"key":"S","label":"S"}}""");
        var resultado = _validador.Validar(ruta);
        Assert.NotEmpty(resultado.Problemas);
    }

    private string Fabricar(string json)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"tabla-{Guid.NewGuid():N}.json");
        File.WriteAllText(ruta, json);
        _temporales.Add(ruta);
        return ruta;
    }

    public void Dispose()
    {
        foreach (var ruta in _temporales) File.Delete(ruta);
    }
}

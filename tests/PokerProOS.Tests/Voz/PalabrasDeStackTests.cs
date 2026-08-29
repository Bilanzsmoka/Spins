using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

/// <summary>
/// Agregar una palabra de stack.
///
/// No se podía, y el error que salía a pantalla era
/// «The node must be of type 'JsonObject'»: `palabrasDeStack` es una lista
/// plana de textos, pero la guarda contra formas repetidas recorría las
/// entradas asumiendo que todas son objetos con `clave` y `dichos`. Al
/// toparse con un texto, reventaba.
///
/// Que la excepción del serializador llegara tal cual a la pantalla es el
/// segundo defecto: no dice qué pasó ni qué hacer.
/// </summary>
public class PalabrasDeStackTests : IDisposable
{
    /// <summary>
    /// La lista es plana y no tiene claves, así que el editor la identifica
    /// por el nombre de su propiedad. Es la misma constante que manda la
    /// pantalla.
    /// </summary>
    private const string ClaveDeStack = "palabrasDeStack";

    private readonly List<string> _temporales = [];

    [Fact]
    public async Task Se_puede_agregar_una_palabra_de_stack()
    {
        var (editor, ruta) = Armar();

        var resultado = await editor.AgregarAsync(
            CategoriaDeVocabulario.PalabrasDeStack, ClaveDeStack, "fichitas", default);

        Assert.True(resultado.Exito, resultado.Error);
        Assert.Contains("fichitas", RegistroDeVocabularioJson.Cargar(ruta).PalabrasDeStack);
    }

    /// <summary>
    /// Y una vez agregada tiene que servir para dictar: una palabra de stack
    /// que se guarda pero no se entiende no agrega nada.
    /// </summary>
    [Fact]
    public async Task La_palabra_agregada_sirve_para_dictar_un_stack()
    {
        var (editor, ruta) = Armar();
        await editor.AgregarAsync(
            CategoriaDeVocabulario.PalabrasDeStack, ClaveDeStack, "fichitas", default);

        var dictado = new InterpretadorDeTexto(RegistroDeVocabularioJson.Cargar(ruta))
            .Interpretar("doce fichitas", 0.9f);

        Assert.NotNull(dictado);
        Assert.Equal(12m, dictado.StackBB);
    }

    /// <summary>
    /// Repetir una que ya está se rechaza con un mensaje entendible, no con
    /// una excepción del serializador.
    /// </summary>
    [Fact]
    public async Task Una_palabra_repetida_se_rechaza_con_un_mensaje_claro()
    {
        var (editor, _) = Armar();

        var resultado = await editor.AgregarAsync(
            CategoriaDeVocabulario.PalabrasDeStack, ClaveDeStack, "blinds", default);

        Assert.False(resultado.Exito);
        Assert.Contains("ya estaba", resultado.Error);
    }

    private (EditorDeVocabularioJson Editor, string Ruta) Armar()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"vocabulario-{Guid.NewGuid():N}.json");
        File.Copy(Rutas.Registro("vocabulario.json"), ruta);
        _temporales.Add(ruta);

        var vivo = new VocabularioVivo(RegistroDeVocabularioJson.Cargar(ruta));
        return (new EditorDeVocabularioJson(ruta, vivo), ruta);
    }

    public void Dispose()
    {
        foreach (var ruta in _temporales) File.Delete(ruta);
        GC.SuppressFinalize(this);
    }
}

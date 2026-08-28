using System.Text.Json;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

/// <summary>
/// Una mano entera dicha de una sola forma.
///
/// Enseñar rangos sueltos es lo que generaliza —una forma nueva de "nueve"
/// arregla todas las manos con un nueve— pero no siempre alcanza: el
/// navegador funde "as rey" en algo que no se puede partir en dos, y ahí no
/// hay rango que enseñar. Esta categoría es esa salida.
///
/// Es la única cuyas claves no están listadas de antemano: son las 169 de la
/// matriz, y ninguna aparece en el archivo hasta que alguien la enseña.
/// </summary>
public class ManosHabladasTests : IDisposable
{
    private readonly List<string> _temporales = [];

    [Fact]
    public void Una_forma_de_mano_resuelve_los_dos_rangos_y_el_palo()
    {
        var interprete = new InterpretadorDeTexto(
            new VocabularioConManos([new FormasHabladas("AKo", ["vivir race"])]));

        var d = interprete.Interpretar("vivir race", 0.9f)!;

        Assert.Equal("A", d.RangoAlto);
        Assert.Equal("K", d.RangoBajo);
        Assert.Equal("o", d.Palo);
    }

    /// <summary>
    /// Un par no lleva palo: son dos cartas del mismo rango y no hay suited
    /// ni offsuit que elegir. Devolver uno inventado mandaría a resolver una
    /// casilla que no existe.
    /// </summary>
    [Fact]
    public void Un_par_dicho_entero_no_trae_palo()
    {
        var interprete = new InterpretadorDeTexto(
            new VocabularioConManos([new FormasHabladas("AA", ["ases"])]));

        var d = interprete.Interpretar("ases", 0.9f)!;

        Assert.Equal("A", d.RangoAlto);
        Assert.Equal("A", d.RangoBajo);
        Assert.Null(d.Palo);
    }

    /// <summary>
    /// Sin una forma enseñada, el texto se sigue rechazando: la categoría no
    /// afloja el filtro, solo agrega lo que se le enseñó.
    /// </summary>
    [Fact]
    public void Sin_forma_ensenada_el_texto_se_sigue_rechazando()
    {
        var interprete = new InterpretadorDeTexto(new VocabularioConManos([]));

        Assert.Null(interprete.Interpretar("vivir race", 0.9f));
    }

    [Fact]
    public async Task Una_mano_que_no_estaba_en_el_archivo_se_crea_al_guardarla()
    {
        var (editor, ruta) = Armar();

        var resultado = await editor.AgregarAsync(
            CategoriaDeVocabulario.Manos, "AKo", "Vivir Race.", default);

        Assert.True(resultado.Exito, resultado.Error);
        var manos = RegistroDeVocabularioJson.Cargar(ruta).Manos;
        var ako = Assert.Single(manos);
        Assert.Equal("AKo", ako.Clave);
        // Guardada normalizada, igual que el resto: el dictado llega con
        // mayúsculas y punto final, y el intérprete compara sin nada de eso.
        Assert.Equal("vivir race", Assert.Single(ako.Dichos));
    }

    [Fact]
    public async Task Una_clave_que_no_es_de_las_169_se_rechaza()
    {
        var (editor, _) = Armar();

        var resultado = await editor.AgregarAsync(
            CategoriaDeVocabulario.Manos, "KAo", "vivir race", default);

        Assert.False(resultado.Exito);
        Assert.Contains("169", resultado.Error);
    }

    /// <summary>
    /// Quitar la última forma de una mano se permite —siempre se la puede
    /// nombrar por sus dos rangos—, al revés que en una situación, donde
    /// quedarse sin formas es quedarse sin manera de decirla.
    /// </summary>
    [Fact]
    public async Task Se_puede_quitar_la_ultima_forma_de_una_mano()
    {
        var (editor, ruta) = Armar();
        await editor.AgregarAsync(CategoriaDeVocabulario.Manos, "AKo", "vivir race", default);

        var resultado = await editor.QuitarAsync(
            CategoriaDeVocabulario.Manos, "AKo", "vivir race", default);

        Assert.True(resultado.Exito, resultado.Error);
        // Y la entrada se va con ella. Una mano sin formas no significa nada,
        // y vocabulario.json es un archivo que se edita a mano: dejar
        // esqueletos vacios lo va ensuciando con cada correccion.
        Assert.Empty(RegistroDeVocabularioJson.Cargar(ruta).Manos);
    }

    private (EditorDeVocabularioJson Editor, string Ruta) Armar()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"vocabulario-{Guid.NewGuid():N}.json");
        File.Copy(Rutas.Registro("vocabulario.json"), ruta);
        // Se arranca desde una copia sin manos: el archivo del repo no las
        // trae, que es exactamente el estado que hay que poder editar.
        var raiz = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            File.ReadAllText(ruta))!;
        raiz.Remove("manos");
        File.WriteAllText(ruta, JsonSerializer.Serialize(raiz));
        _temporales.Add(ruta);

        var vivo = new VocabularioVivo(RegistroDeVocabularioJson.Cargar(ruta));
        return (new EditorDeVocabularioJson(ruta, vivo), ruta);
    }

    public void Dispose()
    {
        foreach (var ruta in _temporales) File.Delete(ruta);
        GC.SuppressFinalize(this);
    }

    /// <summary>El vocabulario real más las manos que la prueba quiera.</summary>
    private sealed class VocabularioConManos(IReadOnlyList<FormasHabladas> manos)
        : IRegistroDeVocabulario
    {
        private readonly IRegistroDeVocabulario _real =
            RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));

        public IReadOnlyList<string> PalabrasDeStack => _real.PalabrasDeStack;
        public IReadOnlyList<FormasHabladas> Rangos => _real.Rangos;
        public IReadOnlyList<FormasHabladas> Palos => _real.Palos;
        public IReadOnlyList<FormasHabladas> Spots => _real.Spots;
        public IReadOnlyList<FormasHabladas> Situaciones => _real.Situaciones;
        public IReadOnlyList<FormasHabladas> Formatos => _real.Formatos;
        public IReadOnlyList<FormasHabladas> Manos { get; } = manos;
    }
}

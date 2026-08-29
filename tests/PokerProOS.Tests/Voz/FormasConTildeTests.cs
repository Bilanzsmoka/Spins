using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

/// <summary>
/// Una forma hablada con tilde tiene que servir.
///
/// No servía, y el modo de falla era de los peores: el editor guardaba la
/// forma conservando las tildes, el intérprete se las sacaba a lo que oía
/// pero no a la forma guardada, y entonces comparaba "bebe" contra "bebé" y
/// no coincidía nunca. Enseñabas una palabra, el editor te contestaba que ya
/// estaba, y el dictado seguía respondiendo "no te entendí" para siempre.
///
/// Las tildes no las escribe el usuario: las escribe el reconocedor, que
/// transcribe en castellano. O sea que no había forma de esquivarlo.
///
/// En castellano las tildes están en todos lados, así que esto mataba casi
/// todo lo que se enseñara desde la pantalla.
/// </summary>
public class FormasConTildeTests : IDisposable
{
    private readonly List<string> _temporales = [];

    [Theory]
    [InlineData("ñandú versus ratón fósil")]
    [InlineData("ÑANDÚ VERSUS RATÓN FÓSIL")]
    [InlineData("nandu versus raton fosil")]
    public async Task Una_forma_ensenada_con_tildes_despues_se_entiende(string comoSeDice)
    {
        var (editor, ruta) = Armar();
        var guardado = await editor.AgregarAsync(
            CategoriaDeVocabulario.Situaciones, "HU_BB_VS_MR_FISH", "ñandú versus ratón fósil", default);
        Assert.True(guardado.Exito, guardado.Error);

        var interprete = new InterpretadorDeTexto(RegistroDeVocabularioJson.Cargar(ruta));
        var dictado = interprete.Interpretar(comoSeDice, 0.9f);

        Assert.NotNull(dictado);
        Assert.Equal("HU_BB_VS_MR_FISH", dictado.Situacion);
    }

    /// <summary>
    /// Y "ya estaba" tiene que significar lo mismo en los dos lados: si el
    /// editor considera que dos formas son la misma, el intérprete también.
    /// Si no, el rechazo por duplicado esconde una forma que en realidad no
    /// se puede usar.
    /// </summary>
    [Fact]
    public async Task La_misma_forma_con_y_sin_tildes_no_se_guarda_dos_veces()
    {
        var (editor, _) = Armar();
        await editor.AgregarAsync(
            CategoriaDeVocabulario.Situaciones, "HU_BB_VS_MR_FISH", "ñandú versus ratón fósil", default);

        var segunda = await editor.AgregarAsync(
            CategoriaDeVocabulario.Situaciones, "HU_BB_VS_MR_FISH", "nandu versus raton fosil", default);

        Assert.False(segunda.Exito);
        Assert.Contains("ya estaba", segunda.Error);
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

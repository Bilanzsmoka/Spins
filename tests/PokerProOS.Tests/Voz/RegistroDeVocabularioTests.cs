using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

public class RegistroDeVocabularioTests
{
    private static IRegistroDeVocabulario Cargar() =>
        RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));

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
        var catalogo = new PokerProOS.Infrastructure.Tablas.CargadorDeTablas(
                new PokerProOS.Infrastructure.Tablas.ValidadorDeTabla(
                    PokerProOS.Infrastructure.Tablas.RegistroDeAccionesJson.Cargar(
                        Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas);

        var deLasTablas = catalogo.Situaciones
            .SelectMany(s => s.Stacks).SelectMany(t => t.Spots)
            .Select(s => s.Clave).Distinct().ToHashSet();

        Assert.All(Cargar().Spots, s => Assert.Contains(s.Clave, deLasTablas));
    }
}

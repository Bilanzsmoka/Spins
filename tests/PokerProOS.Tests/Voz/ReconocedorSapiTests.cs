using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;
using PokerProOS.Voz.Sapi;

namespace PokerProOS.Tests.Voz;

public class ReconocedorSapiTests : IDisposable
{
    private readonly List<string> _temporales = [];

    private static (IReconocedorDeVoz Reconocedor, ISintetizadorDeVoz Sintetizador) Armar()
    {
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(
                RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas);
        var vocabulario = RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));
        // Umbral bajo: sobre audio sintetico la confianza queda entre 0,48 y 0,64.
        var opciones = new OpcionesDeVoz { ConfianzaMinima = 0.20f, Voz = "Microsoft Helena Desktop" };
        var gramatica = new GeneradorDeGramatica(catalogo, vocabulario, opciones);
        return (new ReconocedorSapi(gramatica, opciones), new SintetizadorSapi(opciones));
    }

    [Theory]
    [InlineData("siete be be a cinco offsuit", 7, "A", "5", "o")]
    [InlineData("diez be be rey jota suited", 10, "K", "J", "s")]
    [InlineData("cinco be be as as", 5, "A", "A", null)]
    [InlineData("quince be be reina nueve suited", 15, "Q", "9", "s")]
    public void Reconoce_una_frase_dictada(
        string frase, int stack, string alta, string baja, string? palo)
    {
        var (reconocedor, sintetizador) = Armar();
        using (reconocedor)
        using (sintetizador)
        {
            var dictado = reconocedor.ReconocerArchivo(Sintetizar(sintetizador, frase));
            Assert.NotNull(dictado);
            Assert.Equal(stack, dictado!.StackBB);
            Assert.Equal(alta, dictado.RangoAlto);
            Assert.Equal(baja, dictado.RangoBajo);
            Assert.Equal(palo, dictado.Palo);
        }
    }

    [Fact]
    public void Deja_el_stack_nulo_cuando_no_se_dicta()
    {
        var (reconocedor, sintetizador) = Armar();
        using (reconocedor)
        using (sintetizador)
        {
            var dictado = reconocedor.ReconocerArchivo(Sintetizar(sintetizador, "as rey offsuit"));
            Assert.NotNull(dictado);
            Assert.Null(dictado!.StackBB);
            Assert.Equal("A", dictado.RangoAlto);
        }
    }

    [Fact]
    public void No_reconoce_una_frase_fuera_de_la_gramatica()
    {
        var (reconocedor, sintetizador) = Armar();
        using (reconocedor)
        using (sintetizador)
        {
            var wav = Sintetizar(sintetizador, "mañana voy al supermercado a comprar pan");
            Assert.Null(reconocedor.ReconocerArchivo(wav));
        }
    }

    [Fact]
    public void Pausar_y_reanudar_repetidamente_no_lanza()
    {
        // Task 8 ejecuta Pausar(); Hablar(); Reanudar(); en cada consulta de
        // voz, varias veces por minuto. RecognizeAsyncCancel() es asincrono:
        // llamar Reanudar() inmediatamente despues puede pegarle a un motor
        // que todavia no terminó de cancelar. Este bucle ajustado sin espera
        // reproduce esa carrera de forma confiable.
        var (reconocedor, sintetizador) = Armar();
        using (reconocedor)
        using (sintetizador)
        {
            reconocedor.ComenzarEscuchaContinua();

            var excepcion = Record.Exception(() =>
            {
                for (var i = 0; i < 200; i++)
                {
                    reconocedor.Pausar();
                    reconocedor.Reanudar();
                }
            });

            Assert.Null(excepcion);
        }
    }

    [Fact]
    public void La_gramatica_se_construye_desde_el_catalogo()
    {
        var (reconocedor, sintetizador) = Armar();
        using (reconocedor)
        using (sintetizador)
        {
            // 19-99bb existe en las tablas, asi que 80 be be debe entrar en la gramatica.
            var dictado = reconocedor.ReconocerArchivo(
                Sintetizar(sintetizador, "ochenta be be as rey offsuit"));
            Assert.NotNull(dictado);
            Assert.Equal(80, dictado!.StackBB);
        }
    }

    private string Sintetizar(ISintetizadorDeVoz sintetizador, string frase)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"voz-{Guid.NewGuid():N}.wav");
        sintetizador.HablarAArchivo(frase, ruta);
        _temporales.Add(ruta);
        return ruta;
    }

    public void Dispose()
    {
        foreach (var ruta in _temporales)
            if (File.Exists(ruta)) File.Delete(ruta);
    }
}

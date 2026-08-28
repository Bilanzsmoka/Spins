using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

/// <summary>
/// El camino entero sin audio: texto -> intérprete -> copiloto -> respuesta.
/// Es lo que el endpoint va a encadenar.
/// </summary>
public class InterpretarYResolverTests
{
    private static (InterpretadorDeTexto Interprete, CopilotoDeVoz Copiloto) Armar()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var vocabulario = RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);
        var reconocedor = new ReconocedorFalso();
        var copiloto = new CopilotoDeVoz(
            reconocedor,
            new SintetizadorFalso { Reconocedor = reconocedor },
            new ResolverManoHandler(catalogo),
            new RedactorDeRespuesta(acciones, vocabulario),
            new MemoriaDeContexto
            {
                Situacion = "HU_SB_OR_FISH", StackBB = 7, Spot = "SB_OR",
            },
            new AnalizadorDeMemoria(catalogo),
            catalogo);
        return (new InterpretadorDeTexto(vocabulario), copiloto);
    }

    [Fact]
    public void Un_texto_dictado_resuelve_la_mano()
    {
        var (interprete, copiloto) = Armar();
        var dictado = interprete.Interpretar("as as", 0.9f);

        Assert.NotNull(dictado);
        var evento = copiloto.Procesar(dictado!);

        Assert.True(evento.Resuelta);
        Assert.Equal("AA", evento.ManoInterpretada);
    }

    [Fact]
    public void Una_frase_de_conversacion_no_llega_al_copiloto()
        => Assert.Null(Armar().Interprete.Interpretar("contra el limite de gastos", 0.9f));
}

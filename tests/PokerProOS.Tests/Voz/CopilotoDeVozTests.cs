using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

public class CopilotoDeVozTests
{
    private static (CopilotoDeVoz Copiloto, ReconocedorFalso Reconocedor,
                    SintetizadorFalso Sintetizador, MemoriaDeContexto Memoria) Armar()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var vocabulario = RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones))
            .CargarDirectorio(Rutas.SemillasDeTablas);
        var reconocedor = new ReconocedorFalso();
        var sintetizador = new SintetizadorFalso { Reconocedor = reconocedor };
        var memoria = new MemoriaDeContexto
        {
            Situacion = "HU_SB_OR_FISH", StackBB = 7, Spot = "SB_OR"
        };
        var copiloto = new CopilotoDeVoz(
            reconocedor, sintetizador,
            new ResolverManoHandler(catalogo),
            new RedactorDeRespuesta(acciones, vocabulario),
            memoria);
        copiloto.Conectar();
        return (copiloto, reconocedor, sintetizador, memoria);
    }

    private static DictadoReconocido Dictado(
        string alta, string baja, string? palo = null,
        decimal? stack = null, string? spot = null) =>
        new(stack, spot, null, alta, baja, palo, 0.9f, $"{alta} {baja}");

    [Fact]
    public void Usa_el_contexto_en_pantalla_cuando_el_dictado_no_trae_stack()
    {
        var (_, reconocedor, sintetizador, _) = Armar();
        reconocedor.Emitir(Dictado("A", "A"));
        Assert.Single(sintetizador.Dicho);
        Assert.Contains("CALL", sintetizador.Dicho[0]);
    }

    [Fact]
    public void Actualiza_el_contexto_cuando_el_dictado_trae_stack()
    {
        var (_, reconocedor, _, memoria) = Armar();
        reconocedor.Emitir(Dictado("A", "A", stack: 15));
        Assert.Equal(15, memoria.StackBB);
    }

    [Fact]
    public void Actualiza_el_contexto_cuando_el_dictado_trae_spot()
    {
        var (_, reconocedor, _, memoria) = Armar();
        reconocedor.Emitir(Dictado("A", "A", spot: "VS_BB_ALL_IN"));
        Assert.Equal("VS_BB_ALL_IN", memoria.Spot);
    }

    [Fact]
    public void Conserva_el_contexto_entre_consultas_sucesivas()
    {
        var (_, reconocedor, _, memoria) = Armar();
        reconocedor.Emitir(Dictado("A", "A", stack: 15));
        reconocedor.Emitir(Dictado("K", "Q", "s"));
        Assert.Equal(15, memoria.StackBB);
    }

    [Fact]
    public void Pausa_el_reconocedor_mientras_habla_para_no_oirse()
    {
        var (_, reconocedor, sintetizador, _) = Armar();
        reconocedor.Emitir(Dictado("A", "A"));
        Assert.True(sintetizador.PausadoAlHablar[0]);
        Assert.False(reconocedor.Pausado);
    }

    [Fact]
    public void Avisa_cuando_no_entendio()
    {
        var (_, reconocedor, sintetizador, _) = Armar();
        reconocedor.EmitirFallo("ruido");
        Assert.Equal("No te entendí.", sintetizador.Dicho[0]);
    }

    [Fact]
    public void Avisa_cuando_el_spot_no_existe_en_ese_stack()
    {
        var (_, reconocedor, sintetizador, _) = Armar();
        reconocedor.Emitir(Dictado("A", "A", stack: 2, spot: "VS_BB_ISO_3BB"));
        Assert.Contains("no existe", sintetizador.Dicho[0]);
    }

    [Fact]
    public void Publica_un_evento_con_la_mano_interpretada()
    {
        var (copiloto, reconocedor, _, _) = Armar();
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;
        reconocedor.Emitir(Dictado("A", "K"));
        Assert.Equal("AKo", capturado!.ManoInterpretada);
        Assert.True(capturado.Resuelta);
    }

    [Fact]
    public void Publica_un_evento_aunque_no_haya_resuelto()
    {
        var (copiloto, reconocedor, _, _) = Armar();
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;
        reconocedor.EmitirFallo("ruido");
        Assert.False(capturado!.Resuelta);
    }
}

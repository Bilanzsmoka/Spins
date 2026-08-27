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
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
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
            memoria,
            new AnalizadorDeMemoria(catalogo),
            catalogo);
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
    public void Publica_el_codigo_de_accion_cuando_resuelve()
    {
        var (copiloto, reconocedor, _, _) = Armar();
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;
        reconocedor.Emitir(Dictado("A", "K"));
        Assert.True(capturado!.Resuelta);
        Assert.NotEqual("", capturado.Accion);
        // El codigo de accion, no la frase hablada: eso es lo que hace
        // agrupable a la bitacora.
        Assert.DoesNotContain(" ", capturado.Accion);
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

    [Fact]
    public void No_publica_codigo_de_accion_cuando_no_resuelve()
    {
        var (copiloto, reconocedor, _, _) = Armar();
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;
        reconocedor.EmitirFallo("ruido");
        Assert.Equal("", capturado!.Accion);
    }

    [Fact]
    public void Publica_el_evento_antes_de_hablar()
    {
        var (copiloto, reconocedor, sintetizador, _) = Armar();
        var orden = new List<string>();
        sintetizador.Orden = orden;
        copiloto.Publicado += (_, __) => orden.Add("publicado");

        reconocedor.Emitir(Dictado("A", "A"));

        Assert.Equal(new[] { "publicado", "hablar" }, orden);
    }

    [Fact]
    public void Publica_el_evento_y_reanuda_el_reconocedor_aunque_hablar_falle()
    {
        var (copiloto, reconocedor, sintetizador, _) = Armar();
        sintetizador.Fallo = new InvalidOperationException("síntesis fallida");
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;

        var evento = copiloto.Procesar(Dictado("A", "A"));

        Assert.Equal(evento, capturado);
        Assert.False(reconocedor.Pausado);
    }

    [Fact]
    public void Avisa_el_fallo_de_sintesis()
    {
        var (copiloto, _, sintetizador, _) = Armar();
        var fallo = new InvalidOperationException("síntesis fallida");
        sintetizador.Fallo = fallo;
        Exception? capturado = null;
        copiloto.FalloAlHablar += (_, e) => capturado = e;

        copiloto.Procesar(Dictado("A", "A"));

        Assert.Same(fallo, capturado);
    }

    [Fact]
    public void El_evento_trae_la_ficha_de_la_mano_resuelta()
    {
        var (copiloto, _, _, _) = Armar();
        var evento = copiloto.Procesar(Dictado("A", "8", stack: 17, spot: "SB_OR"));

        Assert.NotNull(evento.Ficha);
        Assert.Equal("A8o", evento.Ficha!.Mano);
        Assert.Equal("CALL", evento.Ficha.Accion);
        Assert.NotEmpty(evento.Ficha.Umbral);
    }

    [Fact]
    public void Un_dictado_que_no_resuelve_no_trae_ficha()
    {
        var (copiloto, _, _, _) = Armar();
        var evento = copiloto.Procesar(Dictado("X", "8"));
        Assert.Null(evento.Ficha);
    }

    /// <summary>
    /// Un dictado sin mano es una orden de contexto: "heads up", "contra min
    /// raise", "nueve be be". Mueve la memoria, se confirma hablando y no
    /// resuelve nada, porque no hay mano que resolver.
    /// </summary>
    private static DictadoReconocido Contexto(
        string? situacion = null, decimal? stack = null, string? spot = null) =>
        new(stack, spot, situacion, "", "", null, 0.9f, "contexto");

    [Fact]
    public void Un_dictado_sin_mano_cambia_la_situacion()
    {
        var (copiloto, _, _, memoria) = Armar();
        copiloto.Procesar(Contexto(situacion: "HU_BB_VS_LIMP_FISH"));
        Assert.Equal("HU_BB_VS_LIMP_FISH", memoria.Situacion);
    }

    [Fact]
    public void Un_dictado_sin_mano_cambia_el_stack_y_el_spot()
    {
        var (copiloto, _, _, memoria) = Armar();
        copiloto.Procesar(Contexto(stack: 15, spot: "VS_BB_ALL_IN"));
        Assert.Equal(15, memoria.StackBB);
        Assert.Equal("VS_BB_ALL_IN", memoria.Spot);
    }

    [Fact]
    public void Un_dictado_sin_mano_no_resuelve_ninguna_mano()
    {
        var (copiloto, _, _, _) = Armar();
        var evento = copiloto.Procesar(Contexto(stack: 15));

        Assert.False(evento.Resuelta);
        Assert.Empty(evento.ManoInterpretada);
        Assert.Null(evento.Ficha);
    }

    [Fact]
    public void Un_dictado_sin_mano_se_confirma_hablando()
    {
        var (copiloto, _, sintetizador, _) = Armar();
        copiloto.Procesar(Contexto(situacion: "HU_BB_VS_LIMP_FISH", stack: 15));

        // Contra el mensaje de error, no a favor de una frase exacta: sin esto
        // "Ese spot no existe a 15-17bb" pasaría por confirmación, porque
        // también contiene el 15.
        var dicho = Assert.Single(sintetizador.Dicho);
        Assert.DoesNotContain("no existe", dicho, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No reconozco", dicho, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No tengo tabla", dicho, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No te entendí", dicho, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15", dicho);
    }

    /// <summary>
    /// Cambiar de situación dejaba pegado el spot anterior, que casi nunca
    /// existe en la nueva: a partir de ahí TODA consulta fallaba con "ese spot
    /// no existe" hasta volver a nombrarlo. Ahora cae al primero del stack.
    /// </summary>
    [Fact]
    public void Al_cambiar_de_situacion_un_spot_que_no_existe_cae_al_primero()
    {
        var (copiloto, _, _, memoria) = Armar();
        memoria.Spot = "VS_BB_ISO_ALL_IN"; // solo existe en HU_SB_OR_FISH

        copiloto.Procesar(Contexto(situacion: "HU_BB_VS_LIMP_FISH", stack: 9));

        Assert.Equal("BB_VS_SB_LIMP", memoria.Spot);
    }

    [Fact]
    public void Con_el_spot_corregido_la_mano_siguiente_resuelve()
    {
        var (copiloto, _, _, _) = Armar();
        copiloto.Procesar(Contexto(situacion: "HU_BB_VS_LIMP_FISH", stack: 9));

        var evento = copiloto.Procesar(Dictado("A", "A"));

        Assert.True(evento.Resuelta);
        Assert.Equal("AA", evento.ManoInterpretada);
    }

    [Fact]
    public void Conectar_dos_veces_no_duplica_el_procesamiento()
    {
        var (copiloto, reconocedor, sintetizador, _) = Armar();
        copiloto.Conectar();
        var eventos = 0;
        copiloto.Publicado += (_, __) => eventos++;

        reconocedor.Emitir(Dictado("A", "A"));

        Assert.Single(sintetizador.Dicho);
        Assert.Equal(1, eventos);
    }
}

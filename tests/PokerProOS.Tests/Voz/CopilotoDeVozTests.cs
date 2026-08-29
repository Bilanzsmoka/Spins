using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

public class CopilotoDeVozTests
{
    private static (CopilotoDeVoz Copiloto, MemoriaDeContexto Memoria) Armar()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var vocabulario = RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);
        var memoria = new MemoriaDeContexto
        {
            Situacion = "HU_SB_OR_FISH", StackBB = 7, Spot = "SB_OR"
        };
        var copiloto = new CopilotoDeVoz(
            new ResolverManoHandler(catalogo),
            new RedactorDeRespuesta(acciones, vocabulario),
            memoria,
            new AnalizadorDeMemoria(catalogo),
            catalogo);
        return (copiloto, memoria);
    }

    private static DictadoReconocido Dictado(
        string alta, string baja, string? palo = null,
        decimal? stack = null, string? spot = null) =>
        new(stack, spot, null, null, alta, baja, palo, 0.9f, $"{alta} {baja}");

    [Fact]
    public void Usa_el_contexto_en_pantalla_cuando_el_dictado_no_trae_stack()
    {
        var (copiloto, _) = Armar();
        Assert.Contains("CALL", copiloto.Procesar(Dictado("A", "A")).Respuesta);
    }

    [Fact]
    public void Actualiza_el_contexto_cuando_el_dictado_trae_stack()
    {
        var (copiloto, memoria) = Armar();
        copiloto.Procesar(Dictado("A", "A", stack: 15));
        Assert.Equal(15, memoria.StackBB);
    }

    [Fact]
    public void Actualiza_el_contexto_cuando_el_dictado_trae_spot()
    {
        var (copiloto, memoria) = Armar();
        copiloto.Procesar(Dictado("A", "A", spot: "VS_BB_ALL_IN"));
        Assert.Equal("VS_BB_ALL_IN", memoria.Spot);
    }

    [Fact]
    public void Conserva_el_contexto_entre_consultas_sucesivas()
    {
        var (copiloto, memoria) = Armar();
        copiloto.Procesar(Dictado("A", "A", stack: 15));
        copiloto.Procesar(Dictado("K", "Q", "s"));
        Assert.Equal(15, memoria.StackBB);
    }

    [Fact]
    public void Avisa_cuando_el_spot_no_existe_en_ese_stack()
    {
        var (copiloto, _) = Armar();
        var evento = copiloto.Procesar(Dictado("A", "A", stack: 2, spot: "VS_BB_ISO_3BB"));
        Assert.Contains("no existe", evento.Respuesta);
    }

    [Fact]
    public void Publica_un_evento_con_la_mano_interpretada()
    {
        var (copiloto, _) = Armar();
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;
        copiloto.Procesar(Dictado("A", "K"));
        Assert.Equal("AKo", capturado!.ManoInterpretada);
        Assert.True(capturado.Resuelta);
    }

    [Fact]
    public void Publica_el_codigo_de_accion_cuando_resuelve()
    {
        var (copiloto, _) = Armar();
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;
        copiloto.Procesar(Dictado("A", "K"));
        Assert.True(capturado!.Resuelta);
        Assert.NotEqual("", capturado.Accion);
        // El codigo de accion, no la frase hablada: eso es lo que hace
        // agrupable a la bitacora.
        Assert.DoesNotContain(" ", capturado.Accion);
    }

    [Fact]
    public void Publica_un_evento_aunque_no_haya_resuelto()
    {
        var (copiloto, _) = Armar();
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;
        copiloto.Procesar(Dictado("X", "8"));
        Assert.False(capturado!.Resuelta);
    }

    [Fact]
    public void No_publica_codigo_de_accion_cuando_no_resuelve()
    {
        var (copiloto, _) = Armar();
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;
        copiloto.Procesar(Dictado("X", "8"));
        Assert.Equal("", capturado!.Accion);
    }

    [Fact]
    public void El_evento_trae_la_ficha_de_la_mano_resuelta()
    {
        var (copiloto, _) = Armar();
        var evento = copiloto.Procesar(Dictado("A", "8", stack: 17, spot: "SB_OR"));

        Assert.NotNull(evento.Ficha);
        Assert.Equal("A8o", evento.Ficha!.Mano);
        Assert.Equal("CALL", evento.Ficha.Accion);
        Assert.NotEmpty(evento.Ficha.Umbral);
    }

    [Fact]
    public void Un_dictado_que_no_resuelve_no_trae_ficha()
    {
        var (copiloto, _) = Armar();
        var evento = copiloto.Procesar(Dictado("X", "8"));
        Assert.Null(evento.Ficha);
    }

    /// <summary>
    /// Un dictado sin mano es una orden de contexto: "heads up", "contra min
    /// raise", "nueve be be". Mueve la memoria, se confirma en la respuesta y
    /// no resuelve nada, porque no hay mano que resolver.
    /// </summary>
    private static DictadoReconocido Contexto(
        string? situacion = null, decimal? stack = null, string? spot = null,
        string? formato = null) =>
        new(stack, spot, situacion, formato, "", "", null, 0.9f, "contexto");

    /// <summary>
    /// El formato es el primer escalón: dictarlo tiene que dejar la pantalla en
    /// una tabla de ese formato, no solo guardar la palabra. Sin esto, decir
    /// "tres max" no cambiaba nada visible.
    /// </summary>
    [Fact]
    public void Dictar_un_formato_lleva_a_una_situacion_de_ese_formato()
    {
        var (copiloto, memoria) = Armar();   // arranca en HU_SB_OR_FISH

        copiloto.Procesar(Contexto(formato: "3-max"));

        Assert.StartsWith("3MAX_", memoria.Situacion);
    }

    /// <summary>
    /// La memoria guarda el stack como número (12), pero la pantalla elige por
    /// CLAVE de tabla ("11-12bb"). Sin traducirlo en el evento, dictar un stack
    /// cambiaba la memoria y el selector se quedaba quieto: entendia bien y no
    /// se veia nada.
    /// </summary>
    [Fact]
    public void Una_orden_de_contexto_publica_la_clave_del_stack_que_cubre()
    {
        var (copiloto, _) = Armar();

        var evento = copiloto.Procesar(Contexto(stack: 12));

        Assert.Equal("11-12bb", evento.ClaveDeStack);
    }

    [Fact]
    public void Un_contexto_sin_tabla_para_ese_stack_no_inventa_una_clave()
    {
        var (copiloto, _) = Armar();

        var evento = copiloto.Procesar(Contexto(stack: 250));

        Assert.Null(evento.ClaveDeStack);
    }

    [Fact]
    public void Dictar_el_formato_en_el_que_ya_se_esta_no_mueve_la_situacion()
    {
        var (copiloto, memoria) = Armar();

        copiloto.Procesar(Contexto(formato: "HU"));

        Assert.Equal("HU_SB_OR_FISH", memoria.Situacion);
    }

    [Fact]
    public void Un_dictado_sin_mano_cambia_la_situacion()
    {
        var (copiloto, memoria) = Armar();
        copiloto.Procesar(Contexto(situacion: "HU_BB_VS_LIMP_FISH"));
        Assert.Equal("HU_BB_VS_LIMP_FISH", memoria.Situacion);
    }

    [Fact]
    public void Un_dictado_sin_mano_cambia_el_stack_y_el_spot()
    {
        var (copiloto, memoria) = Armar();
        copiloto.Procesar(Contexto(stack: 15, spot: "VS_BB_ALL_IN"));
        Assert.Equal(15, memoria.StackBB);
        Assert.Equal("VS_BB_ALL_IN", memoria.Spot);
    }

    [Fact]
    public void Un_dictado_sin_mano_no_resuelve_ninguna_mano()
    {
        var (copiloto, _) = Armar();
        var evento = copiloto.Procesar(Contexto(stack: 15));

        Assert.False(evento.Resuelta);
        Assert.Empty(evento.ManoInterpretada);
        Assert.Null(evento.Ficha);
    }

    /// <summary>
    /// La confirmación ya no se oye del lado del servidor: se comprueba sobre
    /// la respuesta publicada, que es lo que el navegador va a decir.
    /// </summary>
    [Fact]
    public void Un_dictado_sin_mano_se_confirma_en_la_respuesta()
    {
        var (copiloto, _) = Armar();
        var evento = copiloto.Procesar(Contexto(situacion: "HU_BB_VS_LIMP_FISH", stack: 15));

        // Contra el mensaje de error, no a favor de una frase exacta: sin esto
        // "Ese spot no existe a 15-17bb" pasaría por confirmación, porque
        // también contiene el 15.
        Assert.DoesNotContain("no existe", evento.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No reconozco", evento.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No tengo tabla", evento.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No te entendí", evento.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15", evento.Respuesta);
    }

    /// <summary>
    /// Cambiar de situación dejaba pegado el spot anterior, que casi nunca
    /// existe en la nueva: a partir de ahí TODA consulta fallaba con "ese spot
    /// no existe" hasta volver a nombrarlo. Ahora cae al primero del stack.
    /// </summary>
    [Fact]
    public void Al_cambiar_de_situacion_un_spot_que_no_existe_cae_al_primero()
    {
        var (copiloto, memoria) = Armar();
        memoria.Spot = "VS_BB_ISO_ALL_IN"; // solo existe en HU_SB_OR_FISH

        copiloto.Procesar(Contexto(situacion: "HU_BB_VS_LIMP_FISH", stack: 9));

        Assert.Equal("BB_VS_SB_LIMP", memoria.Spot);
    }

    [Fact]
    public void Con_el_spot_corregido_la_mano_siguiente_resuelve()
    {
        var (copiloto, _) = Armar();
        copiloto.Procesar(Contexto(situacion: "HU_BB_VS_LIMP_FISH", stack: 9));

        var evento = copiloto.Procesar(Dictado("A", "A"));

        Assert.True(evento.Resuelta);
        Assert.Equal("AA", evento.ManoInterpretada);
    }

    /// <summary>
    /// Una frase rechazada tiene que decirse en voz. Estudiando sin manos, un
    /// cartel en pantalla que nadie está mirando es lo mismo que el silencio:
    /// la respuesta iba vacía y el fallo pasaba desapercibido, así que uno
    /// repetía la mano creyendo que el micrófono no había oído nada.
    /// </summary>
    [Fact]
    public void Una_frase_que_no_se_entiende_se_dice_en_voz()
    {
        var (copiloto, _) = Armar();

        var evento = copiloto.NoEntendido("vivir versus race");

        Assert.Equal(TipoDeDictado.Ignorado, evento.Tipo);
        Assert.Equal("vivir versus race", evento.TextoCrudo);
        Assert.False(string.IsNullOrWhiteSpace(evento.Respuesta));
    }

    /// <summary>
    /// Lo que no se entendió no cambia de tabla. El evento va sin situación,
    /// stack ni spot para que la pantalla no tenga con qué moverse aunque
    /// alguna vez deje de filtrar por tipo.
    /// </summary>
    [Fact]
    public void Una_frase_que_no_se_entiende_no_mueve_la_tabla()
    {
        var (copiloto, memoria) = Armar();
        copiloto.Procesar(Contexto(situacion: "HU_BB_VS_LIMP_FISH", stack: 9));
        var antes = (memoria.Situacion, memoria.StackBB, memoria.Spot);

        var evento = copiloto.NoEntendido("vivir versus race");

        Assert.Equal(antes, (memoria.Situacion, memoria.StackBB, memoria.Spot));
        Assert.Null(evento.Situacion);
        Assert.Null(evento.ClaveDeStack);
        Assert.Null(evento.Spot);
    }

    /// <summary>
    /// Y se publica como cualquier otro evento: es lo que lleva la frase al
    /// SSE, que es de donde la pantalla saca la lista para enseñársela.
    /// </summary>
    [Fact]
    public void Una_frase_que_no_se_entiende_se_publica()
    {
        var (copiloto, _) = Armar();
        EventoDeCopiloto? publicado = null;
        copiloto.Publicado += (_, e) => publicado = e;

        copiloto.NoEntendido("vivir versus race");

        Assert.NotNull(publicado);
        Assert.Equal(TipoDeDictado.Ignorado, publicado.Tipo);
    }

    /// <summary>
    /// Una mano dictada sin palo se asume offsuit —es la regla del spec— y el
    /// evento tiene que decirlo. En silencio es una trampa: si el reconocedor
    /// se come el "suited", la consulta resuelve contra la casilla equivocada
    /// y en pantalla no se nota nada raro.
    /// </summary>
    [Fact]
    public void Una_mano_sin_palo_avisa_que_lo_asumio()
    {
        var (copiloto, _) = Armar();

        var evento = copiloto.Procesar(Dictado("A", "K"));

        Assert.True(evento.Resuelta);
        Assert.Equal("AKo", evento.ManoInterpretada);
        Assert.True(evento.PaloAsumido);
    }

    /// <summary>Y si se dictó, no hay nada que avisar.</summary>
    [Fact]
    public void Una_mano_con_palo_dictado_no_avisa_nada()
    {
        var (copiloto, _) = Armar();

        var evento = copiloto.Procesar(Dictado("A", "K", "s"));

        Assert.Equal("AKs", evento.ManoInterpretada);
        Assert.False(evento.PaloAsumido);
    }
}

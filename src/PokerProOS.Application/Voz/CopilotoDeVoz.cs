using PokerProOS.Application.Tablas;

namespace PokerProOS.Application.Voz;

/// <summary>Las tres cosas que puede ser un dictado.</summary>
public enum TipoDeDictado
{
    /// <summary>Trajo una mano y la tabla la resolvió.</summary>
    Mano,
    /// <summary>Cambió la tabla activa sin nombrar una mano.</summary>
    Contexto,
    /// <summary>No era una orden: conversación cerca del micrófono.</summary>
    Ignorado,
}

public record EventoDeCopiloto(
    string TextoCrudo,
    string ManoInterpretada,
    string Accion,
    string Respuesta,
    bool Resuelta,
    /// <summary>
    /// Qué clase de dictado fue. <c>Resuelta</c> sola no alcanza: una orden de
    /// contexto ("doce blinds") se entiende perfectamente y no resuelve
    /// ninguna mano, así que la pantalla la mostraba como "no entendí" y no
    /// movía los selectores. Son tres resultados, no dos.
    /// </summary>
    TipoDeDictado Tipo,
    string? Situacion,
    string? ClaveDeStack,
    string? Spot,
    /// <summary>
    /// Lo que hay que saber de esa mano, para leer. Nulo si el dictado no
    /// resolvió: no hay nada que explicar de una mano que no se entendió.
    /// </summary>
    FichaDeMemoria? Ficha = null,
    /// <summary>
    /// El palo no se dictó y se asumió offsuit, que es la regla del spec.
    ///
    /// Viaja hasta la pantalla porque en silencio es una trampa: si el
    /// reconocedor se come el "suited" —cosa que pasa seguido—, la consulta
    /// resuelve contra la casilla equivocada y todo se ve normal. La voz ya
    /// avisaba deletreando la mano, pero estudiando de memoria uno mira la
    /// grilla, no escucha.
    /// </summary>
    bool PaloAsumido = false);

/// <summary>
/// Une la memoria de contexto, el resolvedor de tabla y el redactor: recibe un
/// dictado ya interpretado, lo resuelve contra la última situación/stack/spot
/// conocidos y publica un evento para que la pantalla resalte la celda,
/// resuelva o no. Oír y hablar son del navegador: acá no entra audio.
/// </summary>
public sealed class CopilotoDeVoz(
    ResolverManoHandler resolver,
    RedactorDeRespuesta redactor,
    MemoriaDeContexto memoria,
    AnalizadorDeMemoria analizador,
    ICatalogoDeTablas catalogo)
{
    public event EventHandler<EventoDeCopiloto>? Publicado;

    public EventoDeCopiloto Procesar(DictadoReconocido dictado)
    {
        MudarDeFormato(dictado);
        memoria.Aplicar(dictado);
        AcomodarElSpotRecordado(dictado);

        // Un dictado sin mano es una orden de contexto ("heads up", "contra
        // min raise", "nueve be be"): mueve la memoria y se confirma, pero no
        // hay nada que resolver. Sin esto habría que nombrar una mano en cada
        // frase solo para poder cambiar de tabla.
        if (SinMano(dictado)) return PublicarYDevolver(Confirmacion(dictado));

        var resultado = resolver.Resolver(new ConsultaDeMano(
            memoria.Situacion, memoria.StackBB, memoria.Spot,
            dictado.RangoAlto, dictado.RangoBajo, dictado.Palo));

        // Resolver y explicar son dos cosas distintas: ResolverManoHandler
        // sigue respondiendo "qué hago" y el analizador agrega el "por qué".
        var ficha = resultado.Respuesta is null
            ? null
            : analizador.Analizar(
                memoria.Situacion, resultado.Respuesta.ClaveDeStack,
                memoria.Spot, resultado.Respuesta.Mano);

        var evento = new EventoDeCopiloto(
            dictado.TextoCrudo,
            resultado.Respuesta?.Mano ?? "",
            resultado.Respuesta?.Accion ?? "",
            redactor.Redactar(resultado),
            resultado.Respuesta is not null,
            TipoDeDictado.Mano,
            memoria.Situacion,
            resultado.Respuesta?.ClaveDeStack,
            memoria.Spot,
            ficha,
            resultado.Respuesta?.PaloAsumido ?? false);

        Publicar(evento);
        return evento;
    }

    /// <summary>
    /// La frase que el intérprete rechazó. Es un resultado del copiloto como
    /// cualquier otro, no un descarte: se publica —para que la pantalla la
    /// junte y se la pueda enseñar después— y se dice en voz, porque
    /// estudiando sin manos un cartel que nadie mira es lo mismo que el
    /// silencio, y sin oír nada uno repite la mano creyendo que el micrófono
    /// no captó.
    ///
    /// Va sin situación, stack ni spot: lo que no se entendió no puede mover
    /// la tabla que estabas mirando.
    /// </summary>
    public EventoDeCopiloto NoEntendido(string texto) => PublicarYDevolver(new EventoDeCopiloto(
        texto, "", "", redactor.RedactarNoEntendido(),
        false, TipoDeDictado.Ignorado, null, null, null));

    /// <summary>
    /// Dictar un formato ("heads up", "tres max") es pedir cambiar de mesa, no
    /// guardar una palabra: mueve la situación a una de ese formato. Es el
    /// primer escalón para cambiar de tabla hablando, y sin esto decir el
    /// formato no producía nada visible.
    ///
    /// Si ya se está en ese formato no se toca nada: repetirlo para confirmar
    /// no puede sacar al usuario de la tabla que venía mirando.
    /// </summary>
    private void MudarDeFormato(DictadoReconocido dictado)
    {
        if (dictado.Formato is not { Length: > 0 } formato) return;

        var actual = catalogo.Situacion(memoria.Situacion);
        if (actual is not null && Mismo(actual.Formato, formato)) return;

        var destino = catalogo.Situaciones.FirstOrDefault(s => Mismo(s.Formato, formato));
        if (destino is null) return;

        memoria.Situacion = destino.Clave;
        // La tabla nueva casi nunca tiene el spot de la anterior; el primero de
        // su stack es lo que la pantalla ya elige en el mismo caso.
        memoria.Spot = catalogo.StackQueCubre(destino.Clave, memoria.StackBB)?
            .Spots.FirstOrDefault()?.Clave ?? memoria.Spot;
    }

    private static bool Mismo(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool SinMano(DictadoReconocido dictado)
        => string.IsNullOrWhiteSpace(dictado.RangoAlto)
           && string.IsNullOrWhiteSpace(dictado.RangoBajo);

    /// <summary>
    /// El spot que quedó de la consulta anterior casi nunca existe en la
    /// situación nueva: los de heads-up no están en 3-max, y los de un stack
    /// corto tampoco en uno largo. Sin esto, cambiar de tabla dejaba TODA
    /// consulta posterior respondiendo "ese spot no existe" hasta volver a
    /// nombrarlo a mano. Cae al primero del stack que cubre, igual que hacen
    /// los selectores de la pantalla.
    ///
    /// Solo corrige lo <b>recordado</b>: si el dictado nombró el spot, un
    /// spot inexistente es un error del usuario y tiene que oírlo, no que se
    /// lo cambien por otro en silencio.
    /// </summary>
    private void AcomodarElSpotRecordado(DictadoReconocido dictado)
    {
        if (dictado.Spot is { Length: > 0 }) return;

        var tabla = catalogo.StackQueCubre(memoria.Situacion, memoria.StackBB);
        if (tabla is null || tabla.Spot(memoria.Spot) is not null) return;

        memoria.Spot = tabla.Spots.FirstOrDefault()?.Clave ?? memoria.Spot;
    }

    private EventoDeCopiloto Confirmacion(DictadoReconocido dictado) => new(
        dictado.TextoCrudo,
        "",
        "",
        redactor.RedactarContexto(
            dictado.Situacion, dictado.StackBB, dictado.Spot, dictado.Formato),
        false,
        TipoDeDictado.Contexto,
        memoria.Situacion,
        // La memoria lleva el stack en BB (12) y la pantalla elige por clave de
        // tabla ("11-12bb"). Sin traducirlo acá, dictar un stack cambiaba la
        // memoria y el selector se quedaba quieto: entendía bien y no se veía
        // nada. Nulo si ninguna tabla cubre ese número, para no inventar una.
        catalogo.StackQueCubre(memoria.Situacion, memoria.StackBB)?.Stack.Clave,
        memoria.Spot);

    private EventoDeCopiloto PublicarYDevolver(EventoDeCopiloto evento)
    {
        Publicar(evento);
        return evento;
    }

    /// <summary>
    /// El único destino del evento es el canal SSE: la pantalla resalta la
    /// celda y el navegador dice la respuesta con su propia voz.
    /// </summary>
    private void Publicar(EventoDeCopiloto evento) => Publicado?.Invoke(this, evento);
}

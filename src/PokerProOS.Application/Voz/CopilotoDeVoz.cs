using PokerProOS.Application.Tablas;

namespace PokerProOS.Application.Voz;

public record EventoDeCopiloto(
    string TextoCrudo,
    string ManoInterpretada,
    string Accion,
    string Respuesta,
    bool Resuelta,
    string? Situacion,
    string? ClaveDeStack,
    string? Spot,
    /// <summary>
    /// Lo que hay que saber de esa mano, para leer. Nulo si el dictado no
    /// resolvió: no hay nada que explicar de una mano que no se entendió.
    /// </summary>
    FichaDeMemoria? Ficha = null);

/// <summary>
/// Une el reconocedor, la memoria de contexto, el resolvedor de tabla y el
/// sintetizador en el bucle del copiloto: escucha, resuelve contra la última
/// situación/stack/spot conocidos, habla la respuesta y publica un evento
/// para que la pantalla resalte la celda, resuelva o no.
/// </summary>
public sealed class CopilotoDeVoz(
    IReconocedorDeVoz reconocedor,
    ISintetizadorDeVoz sintetizador,
    ResolverManoHandler resolver,
    RedactorDeRespuesta redactor,
    MemoriaDeContexto memoria,
    AnalizadorDeMemoria analizador,
    ICatalogoDeTablas catalogo)
{
    private bool _conectado;

    public event EventHandler<EventoDeCopiloto>? Publicado;

    /// <summary>
    /// Se levanta cuando falla la síntesis de la respuesta. El bucle sigue
    /// vivo (el reconocedor se reanuda igual), pero el usuario se quedó sin
    /// voz para esa respuesta y necesita verlo, no descubrirlo hablándole
    /// al vacío.
    /// </summary>
    public event EventHandler<Exception>? FalloAlHablar;

    public void Conectar()
    {
        if (_conectado) return;
        _conectado = true;

        reconocedor.Reconocido += (_, dictado) => Procesar(dictado);
        reconocedor.NoReconocido += (_, crudo) => Publicar(
            new EventoDeCopiloto(crudo, "", "", "No te entendí.", false, null, null, null));
    }

    public EventoDeCopiloto Procesar(DictadoReconocido dictado)
    {
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
            memoria.Situacion,
            resultado.Respuesta?.ClaveDeStack,
            memoria.Spot,
            ficha);

        Publicar(evento);
        return evento;
    }

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
        redactor.RedactarContexto(dictado.Situacion, dictado.StackBB, dictado.Spot),
        false,
        memoria.Situacion,
        null,
        memoria.Spot);

    private EventoDeCopiloto PublicarYDevolver(EventoDeCopiloto evento)
    {
        Publicar(evento);
        return evento;
    }

    /// <summary>
    /// Publica antes de hablar: la pantalla no debería esperar a que termine
    /// la síntesis (del orden de un segundo y medio) para resaltar la celda,
    /// y así el resaltado llega aunque la síntesis falle. Pausa el
    /// reconocedor mientras habla, o se escucha a sí mismo y dispara una
    /// consulta fantasma con su propia respuesta. Si <c>Hablar</c> lanza, la
    /// excepción no debe escapar hacia el callback del reconocedor: eso
    /// tumbaría el bucle entero por una respuesta. Se avisa por
    /// <see cref="FalloAlHablar"/> en su lugar.
    /// </summary>
    private void Publicar(EventoDeCopiloto evento)
    {
        Publicado?.Invoke(this, evento);

        reconocedor.Pausar();
        try
        {
            sintetizador.Hablar(evento.Respuesta);
        }
        catch (Exception ex)
        {
            AvisarFallo(ex);
        }
        finally
        {
            reconocedor.Reanudar();
        }
    }

    private void AvisarFallo(Exception ex)
    {
        try
        {
            FalloAlHablar?.Invoke(this, ex);
        }
        catch
        {
            // Un suscriptor que lanza no puede convertirse en el mismo
            // problema un nivel más arriba.
        }
    }
}

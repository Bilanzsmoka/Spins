using PokerProOS.Application.Tablas;

namespace PokerProOS.Application.Voz;

public record EventoDeCopiloto(
    string TextoCrudo,
    string ManoInterpretada,
    string Respuesta,
    bool Resuelta,
    string? Situacion,
    string? ClaveDeStack,
    string? Spot);

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
    MemoriaDeContexto memoria)
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
            new EventoDeCopiloto(crudo, "", "No te entendí.", false, null, null, null));
    }

    public EventoDeCopiloto Procesar(DictadoReconocido dictado)
    {
        memoria.Aplicar(dictado);

        var resultado = resolver.Resolver(new ConsultaDeMano(
            memoria.Situacion, memoria.StackBB, memoria.Spot,
            dictado.RangoAlto, dictado.RangoBajo, dictado.Palo));

        var evento = new EventoDeCopiloto(
            dictado.TextoCrudo,
            resultado.Respuesta?.Mano ?? "",
            redactor.Redactar(resultado),
            resultado.Respuesta is not null,
            memoria.Situacion,
            resultado.Respuesta?.ClaveDeStack,
            memoria.Spot);

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

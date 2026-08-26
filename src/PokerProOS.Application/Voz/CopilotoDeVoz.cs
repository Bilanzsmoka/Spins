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
    public event EventHandler<EventoDeCopiloto>? Publicado;

    public void Conectar()
    {
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

    private void Publicar(EventoDeCopiloto evento)
    {
        // Pausar mientras habla, o el reconocedor se escucha a sí mismo
        // y dispara una consulta fantasma con su propia respuesta.
        reconocedor.Pausar();
        try
        {
            sintetizador.Hablar(evento.Respuesta);
        }
        finally
        {
            reconocedor.Reanudar();
        }
        Publicado?.Invoke(this, evento);
    }
}

namespace PokerProOS.Application.Voz;

public record DictadoReconocido(
    decimal? StackBB,
    string? Spot,
    string? Situacion,
    string RangoAlto,
    string RangoBajo,
    string? Palo,
    float Confianza,
    string TextoCrudo);

/// <summary>
/// Nadie en la solución lo implementa: oír y hablar son del navegador. El
/// contrato queda porque <c>PokerProOS.Voz.Sapi</c> sigue en el repositorio,
/// fuera de la solución, listo para volver si el navegador decepciona.
/// </summary>
public interface IReconocedorDeVoz : IDisposable
{
    event EventHandler<DictadoReconocido>? Reconocido;
    event EventHandler<string>? NoReconocido;
    void ComenzarEscuchaContinua();
    void Pausar();
    void Reanudar();
    DictadoReconocido? ReconocerArchivo(string rutaWav);

    /// <summary>
    /// Escucha una vez con dictado libre y devuelve el texto crudo, o nulo si
    /// no captó nada. Sirve para aprender cómo dice el usuario algo que la
    /// gramática restringida rechazaría: no importa que el dictado se
    /// equivoque, importa que se equivoque siempre igual.
    /// </summary>
    string? CapturarDictadoLibre(TimeSpan espera);

    /// <summary>Rearma la gramática. Se llama tras editar el vocabulario.</summary>
    void RecargarGramatica();
}

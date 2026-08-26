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

public interface IReconocedorDeVoz : IDisposable
{
    event EventHandler<DictadoReconocido>? Reconocido;
    event EventHandler<string>? NoReconocido;
    void ComenzarEscuchaContinua();
    void Pausar();
    void Reanudar();
    DictadoReconocido? ReconocerArchivo(string rutaWav);
}

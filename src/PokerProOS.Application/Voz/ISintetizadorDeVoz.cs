namespace PokerProOS.Application.Voz;

public interface ISintetizadorDeVoz : IDisposable
{
    void Hablar(string texto);
    void HablarAArchivo(string texto, string rutaWav);
}

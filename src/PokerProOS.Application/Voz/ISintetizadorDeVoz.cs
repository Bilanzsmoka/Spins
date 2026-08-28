namespace PokerProOS.Application.Voz;

/// <summary>
/// Nadie en la solución lo implementa: oír y hablar son del navegador. El
/// contrato queda porque <c>PokerProOS.Voz.Sapi</c> sigue en el repositorio,
/// fuera de la solución, listo para volver si el navegador decepciona.
/// </summary>
public interface ISintetizadorDeVoz : IDisposable
{
    void Hablar(string texto);
    void HablarAArchivo(string texto, string rutaWav);
}

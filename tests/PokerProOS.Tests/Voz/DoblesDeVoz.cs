using PokerProOS.Application.Voz;

namespace PokerProOS.Tests.Voz;

public sealed class ReconocedorFalso : IReconocedorDeVoz
{
    public bool Escuchando { get; private set; }
    public bool Pausado { get; private set; }

    public event EventHandler<DictadoReconocido>? Reconocido;
    public event EventHandler<string>? NoReconocido;

    public void ComenzarEscuchaContinua() => Escuchando = true;
    public void Pausar() => Pausado = true;
    public void Reanudar() => Pausado = false;
    public DictadoReconocido? ReconocerArchivo(string rutaWav) => null;

    public void Emitir(DictadoReconocido dictado) => Reconocido?.Invoke(this, dictado);
    public void EmitirFallo(string texto) => NoReconocido?.Invoke(this, texto);
    public void Dispose() { }
}

public sealed class SintetizadorFalso : ISintetizadorDeVoz
{
    public List<string> Dicho { get; } = [];
    public List<bool> PausadoAlHablar { get; } = [];
    public ReconocedorFalso? Reconocedor { get; set; }

    public void Hablar(string texto)
    {
        Dicho.Add(texto);
        PausadoAlHablar.Add(Reconocedor?.Pausado ?? false);
    }

    public void HablarAArchivo(string texto, string rutaWav) => Dicho.Add(texto);
    public void Dispose() { }
}

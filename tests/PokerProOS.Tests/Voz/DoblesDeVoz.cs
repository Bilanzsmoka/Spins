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

    /// <summary>Lo que el doble devolvera al pedirle un dictado libre.</summary>
    public string? DictadoDevuelto { get; set; }
    public int VecesQueRecargoGramatica { get; private set; }

    public string? CapturarDictadoLibre(TimeSpan espera) => DictadoDevuelto;
    public void RecargarGramatica() => VecesQueRecargoGramatica++;

    public void Emitir(DictadoReconocido dictado) => Reconocido?.Invoke(this, dictado);
    public void EmitirFallo(string texto) => NoReconocido?.Invoke(this, texto);
    public void Dispose() { }
}

public sealed class SintetizadorFalso : ISintetizadorDeVoz
{
    public List<string> Dicho { get; } = [];
    public List<bool> PausadoAlHablar { get; } = [];
    public ReconocedorFalso? Reconocedor { get; set; }

    /// <summary>Cuando se asigna, <see cref="Hablar"/> la lanza en vez de hablar.</summary>
    public Exception? Fallo { get; set; }

    /// <summary>
    /// Lista compartida con el test para registrar en qué orden ocurrieron
    /// las cosas. El test agrega "publicado" al suscribirse a
    /// <c>CopilotoDeVoz.Publicado</c>; este doble agrega "hablar" aquí, en
    /// el mismo instante en que se registra <see cref="PausadoAlHablar"/>.
    /// </summary>
    public List<string>? Orden { get; set; }

    public void Hablar(string texto)
    {
        Orden?.Add("hablar");
        PausadoAlHablar.Add(Reconocedor?.Pausado ?? false);
        if (Fallo is { } fallo) throw fallo;
        Dicho.Add(texto);
    }

    public void HablarAArchivo(string texto, string rutaWav) => Dicho.Add(texto);
    public void Dispose() { }
}

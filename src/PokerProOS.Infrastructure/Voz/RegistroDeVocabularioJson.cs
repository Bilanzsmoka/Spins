using System.Text.Json;
using PokerProOS.Application.Voz;

namespace PokerProOS.Infrastructure.Voz;

public sealed class RegistroDeVocabularioJson : IRegistroDeVocabulario
{
    private RegistroDeVocabularioJson(
        IReadOnlyList<string> palabrasDeStack,
        IReadOnlyList<FormasHabladas> rangos,
        IReadOnlyList<FormasHabladas> palos,
        IReadOnlyList<FormasHabladas> spots,
        IReadOnlyList<FormasHabladas> situaciones)
        => (PalabrasDeStack, Rangos, Palos, Spots, Situaciones)
            = (palabrasDeStack, rangos, palos, spots, situaciones);

    public IReadOnlyList<string> PalabrasDeStack { get; }
    public IReadOnlyList<FormasHabladas> Rangos { get; }
    public IReadOnlyList<FormasHabladas> Palos { get; }
    public IReadOnlyList<FormasHabladas> Spots { get; }
    public IReadOnlyList<FormasHabladas> Situaciones { get; }

    public static IRegistroDeVocabulario Cargar(string ruta)
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(ruta));
            var raiz = documento.RootElement;

            static IReadOnlyList<FormasHabladas> Leer(JsonElement raiz, string propiedad) =>
                raiz.GetProperty(propiedad).EnumerateArray()
                    .Select(e => new FormasHabladas(
                        e.GetProperty("clave").GetString()!,
                        e.GetProperty("dichos").EnumerateArray().Select(d => d.GetString()!).ToList()))
                    .ToList();

            return new RegistroDeVocabularioJson(
                raiz.GetProperty("palabrasDeStack").EnumerateArray().Select(e => e.GetString()!).ToList(),
                Leer(raiz, "rangos"),
                Leer(raiz, "palos"),
                Leer(raiz, "spots"),
                Leer(raiz, "situaciones"));
        }
        catch (Exception ex)
        {
            throw new RegistroInvalidoException(ruta, ex);
        }
    }
}

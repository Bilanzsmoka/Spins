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
        IReadOnlyList<FormasHabladas> situaciones,
        IReadOnlyList<FormasHabladas> formatos)
        => (PalabrasDeStack, Rangos, Palos, Spots, Situaciones, Formatos)
            = (palabrasDeStack, rangos, palos, spots, situaciones, formatos);

    public IReadOnlyList<string> PalabrasDeStack { get; }
    public IReadOnlyList<FormasHabladas> Rangos { get; }
    public IReadOnlyList<FormasHabladas> Palos { get; }
    public IReadOnlyList<FormasHabladas> Spots { get; }
    public IReadOnlyList<FormasHabladas> Situaciones { get; }
    public IReadOnlyList<FormasHabladas> Formatos { get; }

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
                Leer(raiz, "situaciones"),
                // Opcional: un vocabulario viejo no lo tiene y la app tiene que
                // arrancar igual, solo sin poder dictar el formato.
                raiz.TryGetProperty("formatos", out _) ? Leer(raiz, "formatos") : []);
        }
        catch (Exception ex)
        {
            throw new RegistroInvalidoException(ruta, ex);
        }
    }
}

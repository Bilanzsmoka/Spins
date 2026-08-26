using System.Text.Json;
using PokerProOS.Application.Diario;

namespace PokerProOS.Infrastructure.Diario;

public sealed class RegistroDeHabitosJson : IRegistroDeHabitos
{
    private readonly HashSet<string> _claves;

    private RegistroDeHabitosJson(IReadOnlyList<HabitoDefinido> todos)
    {
        Todos = todos;
        _claves = todos.Select(h => h.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<HabitoDefinido> Todos { get; }

    public bool Existe(string clave) => _claves.Contains(clave);

    public static IRegistroDeHabitos Cargar(string ruta)
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(ruta));
            var habitos = documento.RootElement.GetProperty("habitos").EnumerateArray()
                .Select(e => new HabitoDefinido(
                    e.GetProperty("clave").GetString()!,
                    e.GetProperty("etiqueta").GetString()!,
                    e.GetProperty("tipo").GetString()!,
                    e.GetProperty("orden").GetInt32(),
                    e.TryGetProperty("ayuda", out var ayuda) ? ayuda.GetString() ?? "" : "",
                    e.TryGetProperty("invertido", out var inv) && inv.GetBoolean()))
                .OrderBy(h => h.Orden)
                .ToList();
            return new RegistroDeHabitosJson(habitos);
        }
        catch (Exception ex)
        {
            throw new RegistroInvalidoException(ruta, ex);
        }
    }
}

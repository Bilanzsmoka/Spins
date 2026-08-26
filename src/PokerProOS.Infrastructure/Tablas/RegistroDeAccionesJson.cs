using System.Text.Json;
using PokerProOS.Application.Tablas;

namespace PokerProOS.Infrastructure.Tablas;

public sealed class RegistroDeAccionesJson : IRegistroDeAcciones
{
    private readonly Dictionary<string, AccionDefinida> _porClave;

    private RegistroDeAccionesJson(IReadOnlyList<AccionDefinida> acciones)
    {
        Todas = acciones;
        _porClave = acciones.ToDictionary(a => a.Clave, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<AccionDefinida> Todas { get; }

    public bool Existe(string clave) => _porClave.ContainsKey(clave);

    public AccionDefinida Obtener(string clave) => _porClave.TryGetValue(clave, out var accion)
        ? accion
        : throw new KeyNotFoundException(
            $"La acción '{clave}' no está en el registro. Agregala a database/registro/acciones.json.");

    public static IRegistroDeAcciones Cargar(string rutaArchivo)
    {
        using var documento = JsonDocument.Parse(File.ReadAllText(rutaArchivo));
        var acciones = documento.RootElement.GetProperty("acciones")
            .EnumerateArray()
            .Select(e => new AccionDefinida(
                e.GetProperty("clave").GetString()!,
                e.GetProperty("etiqueta").GetString()!,
                e.GetProperty("color").GetString()!,
                e.GetProperty("colorTexto").GetString()!,
                e.GetProperty("orden").GetInt32(),
                e.GetProperty("dichos").EnumerateArray().Select(d => d.GetString()!).ToList()))
            .OrderBy(a => a.Orden)
            .ToList();
        return new RegistroDeAccionesJson(acciones);
    }
}

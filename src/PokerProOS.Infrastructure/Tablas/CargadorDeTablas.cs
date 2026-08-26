using System.Text.Json;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;

namespace PokerProOS.Infrastructure.Tablas;

public sealed class CargadorDeTablas(ValidadorDeTabla validador)
{
    public ICatalogoDeTablas CargarDirectorio(string directorio)
    {
        if (!Directory.Exists(directorio))
            return new CatalogoEnMemoria([], [new ProblemaDeTabla(
                directorio, "", "", $"No existe el directorio de tablas: {directorio}")]);

        var problemas = new List<ProblemaDeTabla>();
        var stacksPorSituacion = new Dictionary<string, (string Etiqueta, List<TablaDeStack> Stacks)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var archivo in Directory.GetFiles(directorio, "*.json").OrderBy(a => a))
        {
            var validacion = validador.Validar(archivo);
            if (!validacion.EsValido)
            {
                problemas.AddRange(validacion.Problemas);
                continue;
            }
            LeerArchivo(archivo, stacksPorSituacion);
        }

        var situaciones = stacksPorSituacion
            .Select(par => new SituacionDeTabla(
                par.Key,
                par.Value.Etiqueta,
                par.Value.Stacks.OrderBy(t => t.Stack.MinBB).ToList()))
            .ToList();

        return new CatalogoEnMemoria(situaciones, problemas);
    }

    private static void LeerArchivo(
        string archivo,
        Dictionary<string, (string Etiqueta, List<TablaDeStack> Stacks)> acumulador)
    {
        using var documento = JsonDocument.Parse(File.ReadAllText(archivo));
        var raiz = documento.RootElement;
        var situacion = raiz.GetProperty("situation");
        var claveSituacion = situacion.GetProperty("key").GetString()!;
        var etiquetaSituacion = situacion.GetProperty("label").GetString()!;

        if (!acumulador.TryGetValue(claveSituacion, out var entrada))
            acumulador[claveSituacion] = entrada = (etiquetaSituacion, []);

        foreach (var stack in raiz.GetProperty("stacks").EnumerateArray())
        {
            var rango = new RangoDeStack(
                stack.GetProperty("key").GetString()!,
                stack.GetProperty("minBB").GetDecimal(),
                stack.GetProperty("maxBB").GetDecimal());

            var spots = new List<SpotDeTabla>();
            if (stack.TryGetProperty("spots", out var elementosSpot))
                foreach (var spot in elementosSpot.EnumerateArray())
                    spots.Add(LeerSpot(spot));

            entrada.Stacks.Add(new TablaDeStack(rango, spots));
        }
    }

    private static SpotDeTabla LeerSpot(JsonElement spot)
    {
        var asignadas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? resto = null;

        foreach (var propiedad in spot.GetProperty("actions").EnumerateObject())
        {
            if (propiedad.Value.ValueKind == JsonValueKind.String)
            {
                resto = propiedad.Name;
                continue;
            }
            foreach (var elemento in propiedad.Value.EnumerateArray())
                asignadas[elemento.GetString()!] = propiedad.Name;
        }

        if (resto is not null)
            foreach (var mano in MatrizDeManos.Todas())
                asignadas.TryAdd(mano, resto);

        var celdas = MatrizDeManos.Todas()
            .Select(mano => new CeldaDeTabla(mano, asignadas[mano]))
            .ToList();

        return new SpotDeTabla(
            spot.GetProperty("key").GetString()!,
            spot.GetProperty("label").GetString()!,
            celdas);
    }
}

using System.Text.Json;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Manos;

namespace PokerProOS.Infrastructure.Tablas;

public sealed class ValidadorDeTabla(IRegistroDeAcciones registro)
{
    private static readonly HashSet<string> ManosValidas = MatrizDeManos.Todas().ToHashSet();

    public ResultadoDeValidacion Validar(string rutaArchivo)
    {
        var archivo = Path.GetFileName(rutaArchivo);
        var problemas = new List<ProblemaDeTabla>();

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(File.ReadAllText(rutaArchivo));
        }
        catch (JsonException ex)
        {
            return new ResultadoDeValidacion([new ProblemaDeTabla(archivo, "", "", $"JSON inválido: {ex.Message}")]);
        }

        using (documento)
        {
            if (!documento.RootElement.TryGetProperty("stacks", out var stacks))
                return new ResultadoDeValidacion([new ProblemaDeTabla(archivo, "", "", "Falta la propiedad 'stacks'.")]);

            foreach (var stack in stacks.EnumerateArray())
            {
                var claveStack = stack.GetProperty("key").GetString() ?? "";
                if (!stack.TryGetProperty("spots", out var spots)) continue;

                foreach (var spot in spots.EnumerateArray())
                    ValidarSpot(archivo, claveStack, spot, problemas);
            }
        }

        return new ResultadoDeValidacion(problemas);
    }

    private void ValidarSpot(string archivo, string claveStack, JsonElement spot, List<ProblemaDeTabla> problemas)
    {
        var claveSpot = spot.GetProperty("key").GetString() ?? "";
        void Anotar(string mensaje) => problemas.Add(new ProblemaDeTabla(archivo, claveStack, claveSpot, mensaje));

        if (!spot.TryGetProperty("actions", out var acciones))
        {
            Anotar("El spot no declara 'actions'.");
            return;
        }

        var asignadas = new Dictionary<string, string>();
        string? resto = null;

        foreach (var propiedad in acciones.EnumerateObject())
        {
            var accion = propiedad.Name;

            if (!registro.Existe(accion))
            {
                Anotar($"La acción '{accion}' no está en el registro de acciones.");
                continue;
            }

            if (propiedad.Value.ValueKind == JsonValueKind.String)
            {
                if (propiedad.Value.GetString() != "REST")
                {
                    Anotar($"La acción '{accion}' tiene un valor de texto que no es REST.");
                    continue;
                }
                if (resto is not null)
                {
                    Anotar($"Hay dos acciones marcadas como REST: '{resto}' y '{accion}'. Solo puede haber una.");
                    continue;
                }
                resto = accion;
                continue;
            }

            if (propiedad.Value.ValueKind != JsonValueKind.Array)
            {
                Anotar($"La acción '{accion}' no es ni un arreglo de manos ni REST.");
                continue;
            }

            foreach (var elemento in propiedad.Value.EnumerateArray())
            {
                var mano = elemento.GetString();
                if (mano is null) continue;

                if (!ManosValidas.Contains(mano))
                {
                    Anotar($"La mano '{mano}' no existe en la matriz de 169.");
                    continue;
                }
                if (asignadas.TryGetValue(mano, out var previa))
                {
                    Anotar($"La mano '{mano}' está duplicada: aparece en '{previa}' y en '{accion}'.");
                    continue;
                }
                asignadas[mano] = accion;
            }
        }

        if (resto is not null)
            foreach (var mano in ManosValidas)
                asignadas.TryAdd(mano, resto);

        if (asignadas.Count != 169)
            Anotar($"El spot cubre {asignadas.Count} manos y debe cubrir 169. " +
                   "Falta una acción marcada como REST o faltan manos explícitas.");

        var conteos = asignadas.Values
            .GroupBy(a => a)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        if (spot.TryGetProperty("expectedCounts", out var esperados))
            foreach (var esperado in esperados.EnumerateObject())
            {
                var declarado = esperado.Value.GetInt32();
                var real = esperado.Name.Equals("TOTAL", StringComparison.OrdinalIgnoreCase)
                    ? asignadas.Count
                    : conteos.GetValueOrDefault(esperado.Name);
                if (real != declarado)
                    Anotar($"El conteo declarado de '{esperado.Name}' es {declarado} y el real es {real}.");
            }

        if (spot.TryGetProperty("checks", out var comprobaciones))
            foreach (var comprobacion in comprobaciones.EnumerateObject())
            {
                var real = asignadas.GetValueOrDefault(comprobacion.Name);
                var declarada = comprobacion.Value.GetString();
                if (!string.Equals(real, declarada, StringComparison.OrdinalIgnoreCase))
                    Anotar($"El check de '{comprobacion.Name}' declara '{declarada}' y la tabla resuelve '{real}'.");
            }
    }
}

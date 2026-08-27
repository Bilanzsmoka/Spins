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
            // Ningún archivo estructuralmente incompleto debe tumbar la validación de los
            // demás: se degrada a un problema con el máximo contexto disponible en vez de
            // propagar la excepción (Task 4 valida todos los archivos al arrancar).
            try
            {
                if (!documento.RootElement.TryGetProperty("stacks", out var stacks))
                {
                    problemas.Add(new ProblemaDeTabla(archivo, "", "", "Falta la propiedad 'stacks'."));
                    return new ResultadoDeValidacion(problemas);
                }

                foreach (var stack in stacks.EnumerateArray())
                    ValidarStack(archivo, stack, problemas);
            }
            catch (Exception ex)
            {
                problemas.Add(new ProblemaDeTabla(archivo, "", "", $"El archivo no se pudo validar: {ex.Message}"));
            }
        }

        return new ResultadoDeValidacion(problemas);
    }

    private void ValidarStack(string archivo, JsonElement stack, List<ProblemaDeTabla> problemas)
    {
        var claveStackDeclarada = ObtenerClave(stack);
        var claveStack = claveStackDeclarada ?? "";

        try
        {
            if (claveStackDeclarada is null)
                problemas.Add(new ProblemaDeTabla(archivo, claveStack, "", "El stack no declara 'key'."));

            if (!stack.TryGetProperty("spots", out var spots)) return;

            foreach (var spot in spots.EnumerateArray())
                ValidarSpot(archivo, claveStack, spot, problemas);
        }
        catch (Exception ex)
        {
            problemas.Add(new ProblemaDeTabla(archivo, claveStack, "", $"El stack no se pudo validar: {ex.Message}"));
        }
    }

    private void ValidarSpot(string archivo, string claveStack, JsonElement spot, List<ProblemaDeTabla> problemas)
    {
        var claveSpotDeclarada = ObtenerClave(spot);
        var claveSpot = claveSpotDeclarada ?? "";
        void Anotar(string mensaje) => problemas.Add(new ProblemaDeTabla(archivo, claveStack, claveSpot, mensaje));

        if (claveSpotDeclarada is null)
            Anotar("El spot no declara 'key'.");

        try
        {
            ValidarAcciones();
        }
        catch (Exception ex)
        {
            Anotar($"El spot no se pudo validar: {ex.Message}");
        }

        void ValidarAcciones()
        {
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

            // Los mixes se validan ANTES de la cobertura y sus manos entran en
            // `asignadas`: una mano mixta esta cubierta aunque no aparezca en
            // ninguna lista explicita ni haya REST que la alcance.
            if (spot.TryGetProperty("mixes", out var mixes))
            {
                if (mixes.ValueKind != JsonValueKind.Object)
                    Anotar("'mixes' tiene que ser un objeto de mano a frecuencias.");
                else foreach (var entrada in mixes.EnumerateObject())
                {
                    var mano = entrada.Name;
                    if (!ManosValidas.Contains(mano))
                    {
                        Anotar($"El mix declara la mano '{mano}', que no existe en la matriz de 169.");
                        continue;
                    }
                    if (entrada.Value.ValueKind != JsonValueKind.Object)
                    {
                        Anotar($"El mix de '{mano}' tiene que ser un objeto de acción a frecuencia.");
                        continue;
                    }

                    var suma = 0;
                    var partes = 0;
                    string? dominante = null;
                    var mayor = -1;

                    foreach (var parte in entrada.Value.EnumerateObject())
                    {
                        if (!registro.Existe(parte.Name))
                        {
                            Anotar($"El mix de '{mano}' usa la acción '{parte.Name}', que no está en el registro.");
                            continue;
                        }
                        if (!parte.Value.TryGetInt32(out var frecuencia))
                        {
                            Anotar($"La frecuencia de '{parte.Name}' en el mix de '{mano}' no es un número entero.");
                            continue;
                        }
                        suma += frecuencia;
                        partes++;
                        if (frecuencia > mayor) { mayor = frecuencia; dominante = parte.Name; }
                    }

                    if (partes < 2)
                        Anotar($"El mix de '{mano}' declara {partes} acción(es): un mix necesita al menos dos.");
                    else if (suma != 100)
                        Anotar($"Las frecuencias del mix de '{mano}' suman {suma} y deben sumar 100.");

                    // La mano queda contada por su accion dominante, igual que
                    // en el cargador: una celda mixta ocupa un lugar, no dos.
                    if (dominante is not null) asignadas[mano] = dominante;
                }
            }

            if (asignadas.Count != 169)
                Anotar($"El spot cubre {asignadas.Count} manos y debe cubrir 169. " +
                       "Falta una acción marcada como REST o faltan manos explícitas.");

            var conteos = asignadas.Values
                .GroupBy(a => a)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            if (spot.TryGetProperty("expectedCounts", out var esperados))
                foreach (var esperado in esperados.EnumerateObject())
                {
                    if (!esperado.Value.TryGetInt32(out var declarado))
                    {
                        Anotar($"El conteo declarado de '{esperado.Name}' no es un número entero.");
                        continue;
                    }
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
                    var declarada = comprobacion.Value.ValueKind == JsonValueKind.String
                        ? comprobacion.Value.GetString()
                        : null;
                    if (!string.Equals(real, declarada, StringComparison.OrdinalIgnoreCase))
                        Anotar($"El check de '{comprobacion.Name}' declara '{declarada}' y la tabla resuelve '{real}'.");
                }
        }
    }

    /// <summary>
    /// Lee la propiedad 'key' sin lanzar: null si falta o si no es texto.
    /// </summary>
    private static string? ObtenerClave(JsonElement elemento) =>
        elemento.TryGetProperty("key", out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
}

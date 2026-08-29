using System.Text.Json;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;

namespace PokerProOS.Infrastructure.Tablas;

public sealed class CargadorDeTablas(ValidadorDeTabla validador, IRegistroDeAcciones registro)
{
    public ICatalogoDeTablas CargarDirectorio(string directorio)
    {
        if (!Directory.Exists(directorio))
            return new CatalogoEnMemoria([], [new ProblemaDeTabla(
                directorio, "", "", $"No existe el directorio de tablas: {directorio}")]);

        var problemas = new List<ProblemaDeTabla>();
        var stacksPorSituacion =
            new Dictionary<string, (string Etiqueta, string Formato, string? Explicacion,
                List<TablaDeStack> Stacks)>(StringComparer.OrdinalIgnoreCase);

        foreach (var archivo in Directory.GetFiles(directorio, "*.json").OrderBy(a => a))
        {
            var validacion = validador.Validar(archivo);
            if (!validacion.EsValido)
            {
                problemas.AddRange(validacion.Problemas);
                continue;
            }

            // La validación ya garantiza que Validar() en sí no lanza, pero eso
            // no dice nada de LeerArchivo: son dos recorridos independientes del
            // mismo JSON. Un archivo estructuralmente incompleto que pasa la
            // validación (situation/label/minBB ausentes o mal tipados: la
            // validación no los mira) no puede tumbar el resto de las tablas.
            try
            {
                LeerArchivo(archivo, stacksPorSituacion, problemas);
            }
            catch (Exception ex)
            {
                problemas.Add(new ProblemaDeTabla(
                    Path.GetFileName(archivo), "", "", $"El archivo no se pudo leer: {ex.Message}"));
            }
        }

        var situaciones = stacksPorSituacion
            .Select(par => new SituacionDeTabla(
                par.Key,
                par.Value.Etiqueta,
                par.Value.Formato,
                par.Value.Stacks.OrderBy(t => t.Stack.MinBB).ToList(),
                par.Value.Explicacion))
            .ToList();

        return new CatalogoEnMemoria(situaciones, problemas);
    }

    private void LeerArchivo(
        string archivo,
        Dictionary<string, (string Etiqueta, string Formato, string? Explicacion,
            List<TablaDeStack> Stacks)> acumulador,
        List<ProblemaDeTabla> problemas)
    {
        var nombreArchivo = Path.GetFileName(archivo);
        using var documento = JsonDocument.Parse(File.ReadAllText(archivo));
        var raiz = documento.RootElement;
        var situacion = raiz.GetProperty("situation");
        var claveSituacion = situacion.GetProperty("key").GetString()!;
        var etiquetaSituacion = situacion.GetProperty("label").GetString()!;

        // Sin formato declarado la situación seguiría cargando pero quedaría
        // fuera de todo grupo en pantalla, o sea invisible. Cae a "Otros":
        // se ve, se usa, y salta a la vista que al archivo le falta el campo.
        var formato = situacion.TryGetProperty("formato", out var declarado)
            && declarado.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(declarado.GetString())
                ? declarado.GetString()!
                : "Otros";

        // Opcional: sin ella la situación funciona igual, sólo que la pantalla
        // no tiene qué contar de ella.
        var explicacion = situacion.TryGetProperty("explicacion", out var texto)
            && texto.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(texto.GetString())
                ? texto.GetString()
                : null;

        if (!acumulador.TryGetValue(claveSituacion, out var entrada))
            acumulador[claveSituacion] = entrada = (etiquetaSituacion, formato, explicacion, []);

        foreach (var stack in raiz.GetProperty("stacks").EnumerateArray())
        {
            var claveStack = stack.GetProperty("key").GetString()!;

            // Copiar un archivo de chart para iterar sobre él es un flujo
            // normal; si el stack no se renombra, dos archivos declaran la
            // misma clave para la misma situación. Sin esta guarda,
            // StackQueCubre esconde en silencio uno de los dos en el camino
            // de lectura de voz, y las 169 filas duplicadas violan el índice
            // único de ChartStrategyCells recién al sincronizar con SQL
            // Server (después de que SincronizarAsync ya vació la tabla).
            if (entrada.Stacks.Any(t =>
                    string.Equals(t.Stack.Clave, claveStack, StringComparison.OrdinalIgnoreCase)))
            {
                problemas.Add(new ProblemaDeTabla(nombreArchivo, claveStack, "",
                    $"El stack '{claveStack}' ya existe para la situación '{claveSituacion}'; " +
                    "este duplicado se ignora."));
                continue;
            }

            var rango = new RangoDeStack(
                claveStack,
                stack.GetProperty("minBB").GetDecimal(),
                stack.GetProperty("maxBB").GetDecimal());

            var spots = new List<SpotDeTabla>();
            if (stack.TryGetProperty("spots", out var elementosSpot))
                foreach (var spot in elementosSpot.EnumerateArray())
                    spots.Add(LeerSpot(spot));

            entrada.Stacks.Add(new TablaDeStack(rango, spots));
        }
    }

    /// <summary>La de mayor frecuencia; si empatan, la primera declarada.</summary>
    private static string Dominante(IReadOnlyList<ParteDeMix> partes)
        => partes.Aggregate((mejor, parte) => parte.Frecuencia > mejor.Frecuencia ? parte : mejor).Accion;

    /// <summary>
    /// El bloque "mixes" es opcional y es la última palabra sobre esas manos:
    /// pisa lo que les hubiera tocado por lista explícita o por REST.
    /// </summary>
    private Dictionary<string, IReadOnlyList<ParteDeMix>> LeerMixes(JsonElement spot)
    {
        var mixtas = new Dictionary<string, IReadOnlyList<ParteDeMix>>(StringComparer.OrdinalIgnoreCase);
        if (!spot.TryGetProperty("mixes", out var bloque)) return mixtas;

        foreach (var entrada in bloque.EnumerateObject())
        {
            var partes = entrada.Value.EnumerateObject()
                .Select(p => new ParteDeMix(registro.Obtener(p.Name).Clave, p.Value.GetInt32()))
                .ToList();
            if (partes.Count > 0) mixtas[entrada.Name] = partes;
        }
        return mixtas;
    }

    private SpotDeTabla LeerSpot(JsonElement spot)
    {
        var asignadas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? resto = null;

        foreach (var propiedad in spot.GetProperty("actions").EnumerateObject())
        {
            // La validación ya pasó Existe(accion) en modo case-insensitive;
            // Obtener() devuelve la grafía canónica del registro. Guardar esa
            // clave en vez del nombre de la propiedad JSON tal cual es lo que
            // hace que "call" en un chart y "CALL" en acciones.json terminen
            // siendo la misma acción también para el frontend, que sí es
            // sensible a mayúsculas al indexar por clave.
            var clave = registro.Obtener(propiedad.Name).Clave;

            if (propiedad.Value.ValueKind == JsonValueKind.String)
            {
                resto = clave;
                continue;
            }
            foreach (var elemento in propiedad.Value.EnumerateArray())
            {
                // Un elemento JSON null pasa de largo en el validador
                // (ValidarAcciones también lo salta); acá también se ignora
                // en vez de usarlo como clave de diccionario.
                var mano = elemento.GetString();
                if (mano is null) continue;
                asignadas[mano] = clave;
            }
        }

        if (resto is not null)
            foreach (var mano in MatrizDeManos.Todas())
                asignadas.TryAdd(mano, resto);

        // Las manos mixtas pisan lo que les hubiera tocado arriba: el bloque
        // "mixes" es la ultima palabra sobre esa mano.
        var mixtas = LeerMixes(spot);

        var celdas = MatrizDeManos.Todas()
            .Select(mano => mixtas.TryGetValue(mano, out var partes)
                ? new CeldaDeTabla(mano, Dominante(partes), partes)
                : new CeldaDeTabla(mano, registro.Obtener(asignadas[mano]).Clave))
            .ToList();

        return new SpotDeTabla(
            spot.GetProperty("key").GetString()!,
            spot.GetProperty("label").GetString()!,
            celdas,
            spot.TryGetProperty("tip", out var tip) && tip.ValueKind == JsonValueKind.String
                ? tip.GetString()
                : null);
    }
}

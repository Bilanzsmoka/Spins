using System.Text.Json;
using System.Text.Json.Nodes;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Manos;

namespace PokerProOS.Infrastructure.Tablas;

/// <summary>
/// Escribe la edición en el archivo JSON —la fuente de verdad— y recarga el
/// catálogo. Deliberadamente no toca la base de datos: la base es un espejo
/// que se rehace del JSON en cada arranque.
/// </summary>
public sealed class EditorDeTablasJson(
    string directorio,
    CatalogoVivo catalogo,
    CargadorDeTablas cargador) : IEditorDeTablas
{
    // Dos ediciones simultaneas sobre el mismo archivo se pisarian: una lee,
    // la otra lee, las dos escriben y gana la ultima. El editor es de un solo
    // usuario, pero un doble clic alcanza para provocarlo.
    private readonly SemaphoreSlim _turno = new(1, 1);

    public async Task<ResultadoDeEdicion> EditarAsync(EdicionDeCelda edicion, CancellationToken ct)
    {
        if (!MatrizDeManos.Todas().Contains(edicion.Mano))
            return new ResultadoDeEdicion(false, $"La mano '{edicion.Mano}' no existe en la matriz.", []);

        if (edicion.Accion is null && edicion.Mix is null)
            return new ResultadoDeEdicion(false, "Hay que indicar una acción o un mix.", []);

        if (edicion.Mix is { Count: > 0 } mix)
        {
            if (mix.Count < 2)
                return new ResultadoDeEdicion(false, "Un mix necesita al menos dos acciones.", []);
            if (mix.Sum(p => p.Frecuencia) != 100)
                return new ResultadoDeEdicion(false, "Las frecuencias del mix deben sumar 100.", []);
        }

        await _turno.WaitAsync(ct);
        try
        {
            var archivo = UbicarArchivo(edicion.Situacion, edicion.ClaveDeStack);
            if (archivo is null)
                return new ResultadoDeEdicion(false,
                    $"No encontré ningún archivo con {edicion.Situacion} / {edicion.ClaveDeStack}.", []);

            var raiz = JsonNode.Parse(await File.ReadAllTextAsync(archivo, ct))!.AsObject();
            var spot = UbicarSpot(raiz, edicion);
            if (spot is null)
                return new ResultadoDeEdicion(false,
                    $"No encontré {edicion.ClaveDeStack}/{edicion.Spot} en ese archivo.", []);

            Aplicar(spot, edicion);

            // Escribir a un temporal y mover: si el proceso muere a mitad de
            // camino, el archivo original queda intacto en vez de truncado.
            var temporal = archivo + ".tmp";
            await File.WriteAllTextAsync(temporal,
                raiz.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
            File.Move(temporal, archivo, overwrite: true);

            var recargado = cargador.CargarDirectorio(directorio);
            catalogo.Reemplazar(recargado);

            return new ResultadoDeEdicion(true, null, recargado.Problemas);
        }
        finally
        {
            _turno.Release();
        }
    }

    /// <summary>
    /// Busca por situación Y stack, no solo por situación: una misma
    /// situación puede estar repartida en varios archivos —las once tablas
    /// originales del proyecto son un archivo por stack— y quedarse con el
    /// primero que coincida llevaría a editar el archivo equivocado.
    /// </summary>
    private string? UbicarArchivo(string situacion, string claveDeStack)
        => Directory.GetFiles(directorio, "*.json").FirstOrDefault(archivo =>
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(archivo));
                var raiz = doc.RootElement;
                if (raiz.GetProperty("situation").GetProperty("key").GetString() != situacion)
                    return false;
                return raiz.TryGetProperty("stacks", out var stacks)
                    && stacks.EnumerateArray().Any(s =>
                        s.TryGetProperty("key", out var k) && k.GetString() == claveDeStack);
            }
            catch { return false; }
        });

    private static JsonObject? UbicarSpot(JsonObject raiz, EdicionDeCelda edicion)
        => raiz["stacks"]?.AsArray()
            .Select(n => n!.AsObject())
            .FirstOrDefault(s => s["key"]?.GetValue<string>() == edicion.ClaveDeStack)
            ?["spots"]?.AsArray()
            .Select(n => n!.AsObject())
            .FirstOrDefault(s => s["key"]?.GetValue<string>() == edicion.Spot);

    private static void Aplicar(JsonObject spot, EdicionDeCelda edicion)
    {
        var mano = edicion.Mano;
        var acciones = spot["actions"]!.AsObject();

        // 1. Sacar la mano de donde estuviera: de las listas explicitas y de
        //    los mixes. Lo que queda despues es la asignacion limpia.
        foreach (var entrada in acciones)
        {
            if (entrada.Value is not JsonArray lista) continue;
            for (var i = lista.Count - 1; i >= 0; i--)
                if (lista[i]?.GetValue<string>() == mano) lista.RemoveAt(i);
        }
        if (spot["mixes"] is JsonObject mixes) mixes.Remove(mano);

        // 2. Ponerla donde va.
        if (edicion.Mix is { Count: > 1 } partes)
        {
            var bloque = spot["mixes"] as JsonObject;
            if (bloque is null) { bloque = new JsonObject(); spot["mixes"] = bloque; }
            var nuevo = new JsonObject();
            foreach (var parte in partes) nuevo[parte.Accion] = parte.Frecuencia;
            bloque[mano] = nuevo;
        }
        else if (edicion.Accion is { } accion)
        {
            // Si la accion elegida es el REST del spot, no hace falta listarla:
            // la mano cae ahi sola al expandirse.
            var esResto = acciones[accion] is JsonValue v && v.GetValue<string>() == "REST";
            if (!esResto)
            {
                if (acciones[accion] is not JsonArray lista)
                {
                    lista = new JsonArray();
                    acciones[accion] = lista;
                }
                lista.Add(mano);
            }
        }

        // 3. Recalcular los conteos declarados y soltar el check de esa mano:
        //    afirmaba el valor anterior, que el usuario acaba de cambiar.
        RecalcularConteos(spot);
        if (spot["checks"] is JsonObject checks) checks.Remove(mano);

        // Un bloque vacio ensucia el archivo sin aportar nada.
        if (spot["mixes"] is JsonObject vacio && vacio.Count == 0) spot.Remove("mixes");
    }

    private static void RecalcularConteos(JsonObject spot)
    {
        if (spot["expectedCounts"] is null) return;

        var asignadas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? resto = null;

        foreach (var entrada in spot["actions"]!.AsObject())
        {
            if (entrada.Value is JsonValue v && v.GetValue<string>() == "REST") { resto = entrada.Key; continue; }
            if (entrada.Value is not JsonArray lista) continue;
            foreach (var nodo in lista) asignadas[nodo!.GetValue<string>()] = entrada.Key;
        }
        if (resto is not null)
            foreach (var m in MatrizDeManos.Todas()) asignadas.TryAdd(m, resto);

        // Una mano mixta cuenta una sola vez, por su accion dominante: es como
        // cuentan las tablas de origen, donde el total siempre da 169.
        if (spot["mixes"] is JsonObject mixes)
            foreach (var entrada in mixes)
            {
                var partes = entrada.Value!.AsObject()
                    .Select(p => (Accion: p.Key, Frecuencia: p.Value!.GetValue<int>()))
                    .ToList();
                if (partes.Count > 0)
                    asignadas[entrada.Key] = partes.Aggregate(
                        (mejor, p) => p.Frecuencia > mejor.Frecuencia ? p : mejor).Accion;
            }

        var conteos = new JsonObject();
        foreach (var grupo in asignadas.Values.GroupBy(a => a, StringComparer.OrdinalIgnoreCase))
            conteos[grupo.Key] = grupo.Count();
        conteos["TOTAL"] = asignadas.Count;
        spot["expectedCounts"] = conteos;
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using PokerProOS.Application.Voz;

namespace PokerProOS.Infrastructure.Voz;

/// <summary>
/// Enseña a la aplicación cómo dice las cosas este usuario: escribe la forma
/// hablada nueva en vocabulario.json y reemplaza el vocabulario vivo. No hay
/// nada más que avisar: el intérprete lo lee en cada dictado.
/// </summary>
public sealed class EditorDeVocabularioJson(
    string ruta,
    VocabularioVivo vocabulario) : IEditorDeVocabulario
{
    private readonly SemaphoreSlim _turno = new(1, 1);

    private static string Propiedad(CategoriaDeVocabulario categoria) => categoria switch
    {
        CategoriaDeVocabulario.Rangos => "rangos",
        CategoriaDeVocabulario.Palos => "palos",
        CategoriaDeVocabulario.Spots => "spots",
        CategoriaDeVocabulario.Situaciones => "situaciones",
        CategoriaDeVocabulario.PalabrasDeStack => "palabrasDeStack",
        _ => throw new ArgumentOutOfRangeException(nameof(categoria)),
    };

    public Task<ResultadoDeVocabulario> AgregarAsync(
        CategoriaDeVocabulario categoria, string clave, string dicho, CancellationToken ct)
        => EditarAsync(categoria, clave, dicho, agregar: true, ct);

    public Task<ResultadoDeVocabulario> QuitarAsync(
        CategoriaDeVocabulario categoria, string clave, string dicho, CancellationToken ct)
        => EditarAsync(categoria, clave, dicho, agregar: false, ct);

    private async Task<ResultadoDeVocabulario> EditarAsync(
        CategoriaDeVocabulario categoria, string clave, string dicho, bool agregar, CancellationToken ct)
    {
        // El dictado devuelve el texto con mayusculas y puntuacion ("A cinco
        // suite."). El interprete compara en minusculas y sin puntos, asi que
        // se normaliza al guardar: si no, se guardaria una forma que nunca
        // va a coincidir con nada.
        var normalizado = Normalizar(dicho);
        if (normalizado.Length == 0)
            return new ResultadoDeVocabulario(false, "La forma hablada no puede estar vacía.");

        await _turno.WaitAsync(ct);
        try
        {
            var raiz = JsonNode.Parse(await File.ReadAllTextAsync(ruta, ct))!.AsObject();
            var propiedad = Propiedad(categoria);

            var lista = categoria == CategoriaDeVocabulario.PalabrasDeStack
                ? raiz[propiedad]?.AsArray()
                : UbicarDichos(raiz, propiedad, clave);

            if (lista is null)
                return new ResultadoDeVocabulario(false, $"No encontré '{clave}' en {propiedad}.");

            var yaEsta = lista.Any(n => Normalizar(n!.GetValue<string>()) == normalizado);

            if (agregar)
            {
                if (yaEsta) return new ResultadoDeVocabulario(false, $"«{normalizado}» ya estaba.");
                // Una misma forma en dos claves seria ambigua al interpretar:
                // la primera que aparece ganaria, en silencio.
                if (ColisionaEnOtraClave(raiz, propiedad, clave, normalizado, out var otra))
                    return new ResultadoDeVocabulario(false,
                        $"«{normalizado}» ya está en '{otra}'. Una forma no puede significar dos cosas.");
                lista.Add(normalizado);
            }
            else
            {
                if (lista.Count <= 1 && categoria != CategoriaDeVocabulario.PalabrasDeStack)
                    return new ResultadoDeVocabulario(false,
                        "Es la única forma que queda: sin ella no habría manera de decirlo.");
                for (var i = lista.Count - 1; i >= 0; i--)
                    if (Normalizar(lista[i]!.GetValue<string>()) == normalizado) lista.RemoveAt(i);
            }

            var temporal = ruta + ".tmp";
            await File.WriteAllTextAsync(temporal,
                raiz.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
            File.Move(temporal, ruta, overwrite: true);

            vocabulario.Reemplazar(RegistroDeVocabularioJson.Cargar(ruta));

            return new ResultadoDeVocabulario(true, null);
        }
        catch (Exception ex)
        {
            return new ResultadoDeVocabulario(false, ex.Message);
        }
        finally
        {
            _turno.Release();
        }
    }

    private static JsonArray? UbicarDichos(JsonObject raiz, string propiedad, string clave)
        => raiz[propiedad]?.AsArray()
            .Select(n => n!.AsObject())
            .FirstOrDefault(e => e["clave"]?.GetValue<string>() == clave)
            ?["dichos"]?.AsArray();

    private static bool ColisionaEnOtraClave(
        JsonObject raiz, string propiedad, string clave, string dicho, out string? otra)
    {
        otra = null;
        // Solo se compara dentro de la misma categoria. Esto era seguro con
        // la gramatica SRGS, donde cada categoria tenia su posicion fija en la
        // frase. Ya no: InterpretadorDeTexto no tiene posiciones — su
        // ConsumirDichos barre situaciones, spots y palos en una sola pasada
        // de mas larga a mas corta, y entre dichos del mismo largo el
        // desempate lo decide el orden del Concat, en silencio. O sea que un
        // mismo dicho en dos categorias SI compite, y esta guarda no lo ve.
        // Hoy no hay ningun duplicado real entre categorias, asi que el riesgo
        // es latente; si aparece uno, el arreglo es comparar contra las tres
        // categorias que comparten pasada, no solo contra la propia.
        if (raiz[propiedad] is not JsonArray entradas) return false;

        foreach (var nodo in entradas)
        {
            var entrada = nodo!.AsObject();
            var suClave = entrada["clave"]?.GetValue<string>();
            if (suClave == clave) continue;
            if (entrada["dichos"]?.AsArray().Any(d => Normalizar(d!.GetValue<string>()) == dicho) == true)
            {
                otra = suClave;
                return true;
            }
        }
        return false;
    }

    private static string Normalizar(string texto) => new string(
            texto.Trim().ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                .ToArray())
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Aggregate(string.Empty, (acumulado, palabra) =>
            acumulado.Length == 0 ? palabra : $"{acumulado} {palabra}");
}

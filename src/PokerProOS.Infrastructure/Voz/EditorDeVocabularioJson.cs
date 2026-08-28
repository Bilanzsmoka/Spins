using System.Text.Json;
using System.Text.Json.Nodes;
using PokerProOS.Application.Voz;
using PokerProOS.Domain.Manos;

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
        CategoriaDeVocabulario.Formatos => "formatos",
        CategoriaDeVocabulario.Manos => "manos",
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
            // Las manos son las unicas cuya clave no esta listada de antemano
            // —son 169 y ninguna aparece hasta que alguien la ensena—, asi que
            // no hay lista contra la cual validar: se valida contra la matriz.
            if (categoria == CategoriaDeVocabulario.Manos && !MatrizDeManos.Todas().Contains(clave))
                return new ResultadoDeVocabulario(false, $"'{clave}' no es una de las 169 manos.");

            var raiz = JsonNode.Parse(await File.ReadAllTextAsync(ruta, ct))!.AsObject();
            var propiedad = Propiedad(categoria);

            var lista = categoria switch
            {
                CategoriaDeVocabulario.PalabrasDeStack => raiz[propiedad]?.AsArray(),
                // Y por lo mismo, su entrada se crea al guardar la primera
                // forma. Solo al agregar: al quitar, que no exista quiere decir
                // que no habia nada que quitar.
                CategoriaDeVocabulario.Manos => UbicarDichos(raiz, propiedad, clave)
                    ?? (agregar ? CrearEntrada(raiz, propiedad, clave) : null),
                _ => UbicarDichos(raiz, propiedad, clave),
            };

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
                // Las manos quedan fuera de la guarda: quitar la ultima forma
                // de una mano no te deja sin manera de decirla, porque siempre
                // se la puede nombrar por sus dos rangos.
                if (lista.Count <= 1
                    && categoria != CategoriaDeVocabulario.PalabrasDeStack
                    && categoria != CategoriaDeVocabulario.Manos)
                    return new ResultadoDeVocabulario(false,
                        "Es la única forma que queda: sin ella no habría manera de decirlo.");
                for (var i = lista.Count - 1; i >= 0; i--)
                    if (Normalizar(lista[i]!.GetValue<string>()) == normalizado) lista.RemoveAt(i);

                // Una mano sin formas no significa nada, y su entrada solo
                // existia para sostenerlas. vocabulario.json se edita a mano:
                // sin esto, cada correccion le deja un esqueleto vacio.
                if (categoria == CategoriaDeVocabulario.Manos && lista.Count == 0)
                    QuitarEntrada(raiz, propiedad, clave);
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

    /// <summary>Saca del archivo la entrada de una clave que se quedó sin formas.</summary>
    private static void QuitarEntrada(JsonObject raiz, string propiedad, string clave)
    {
        if (raiz[propiedad] is not JsonArray entradas) return;

        for (var i = entradas.Count - 1; i >= 0; i--)
            if (entradas[i]!.AsObject()["clave"]?.GetValue<string>() == clave)
                entradas.RemoveAt(i);
    }

    /// <summary>La entrada vacía de una clave que todavía no estaba en el archivo.</summary>
    private static JsonArray CrearEntrada(JsonObject raiz, string propiedad, string clave)
    {
        if (raiz[propiedad] is not JsonArray entradas)
        {
            entradas = [];
            raiz[propiedad] = entradas;
        }

        var dichos = new JsonArray();
        entradas.Add(new JsonObject { ["clave"] = clave, ["dichos"] = dichos });
        return dichos;
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

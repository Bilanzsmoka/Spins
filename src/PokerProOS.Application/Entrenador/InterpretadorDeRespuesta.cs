using System.Globalization;
using System.Text;
using PokerProOS.Application.Tablas;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// El texto que oyó el navegador, entendido como una respuesta del
/// entrenamiento.
///
/// Es su propia pieza y no un modo de <c>InterpretadorDeTexto</c> a propósito:
/// no hace falta estado. La pantalla de entrenamiento manda su texto a su
/// endpoint, y quién sabe el modo es la pantalla, que ya lo sabe. Un flag
/// global de "estoy entrenando" es una variable más que puede quedar mal.
///
/// Las formas salen de los `dichos` de acciones.json, igual que todo lo demás
/// del proyecto: agregar una manera de decir "all in" no toca código.
/// </summary>
public sealed class InterpretadorDeRespuesta(IRegistroDeAcciones acciones)
{
    /// <summary>La clave de la acción dicha, o null si el texto no es una.</summary>
    public string? Interpretar(string texto)
    {
        var normalizado = Normalizar(texto);
        if (normalizado.Length == 0) return null;

        // De la forma mas larga a la mas corta: si un dicho es prefijo de
        // otro, ganar con el corto se llevaria los dos en silencio.
        var candidatas = acciones.Todas
            .SelectMany(a => a.Dichos.Select(d => (a.Clave, Dicho: Normalizar(d))))
            .Where(c => c.Dicho.Length > 0)
            .OrderByDescending(c => c.Dicho.Length);

        foreach (var (clave, dicho) in candidatas)
            if (normalizado == dicho) return clave;

        return null;
    }

    /// <summary>Minúsculas, sin tildes, sin puntuación y con un solo espacio.</summary>
    private static string Normalizar(string texto)
    {
        var sinTildes = new string((texto ?? "")
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        return string.Join(' ', sinTildes.ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }
}

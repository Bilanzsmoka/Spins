using System.Globalization;
using System.Text;

namespace PokerProOS.Application.Texto;

/// <summary>
/// Cómo se limpia una frase reconocida antes de compararla con el vocabulario:
/// minúsculas, sin tildes y partida por los separadores que mete el navegador.
///
/// Vive suelta y no dentro de un intérprete porque la necesitan dos: el de voz
/// (<c>InterpretadorDeTexto</c>) y el del entrenamiento
/// (<c>InterpretadorDeRespuesta</c>). Eran dos copias carácter por carácter, y
/// agregarle un separador a una habría dejado a los dos intérpretes oyendo
/// distinto sin que nada fallara — es el mismo motivo por el que
/// <c>MatrizDeManos.Partir</c> subió a Domain. Vive en Application y no más
/// abajo porque nadie en Domain lee texto dictado: normalizar lo que se oyó no
/// es una regla del póker.
/// </summary>
public static class NormalizadorDeTexto
{
    /// <summary>
    /// Los separadores. El reconocedor puntúa lo que oye —"siete be be, a rey."—
    /// y esos signos no son parte de ninguna forma hablada.
    /// </summary>
    private static readonly char[] _separadores = [' ', ',', '.', ';', ':', '\t', '\n'];

    /// <summary>Las palabras de la frase, ya normalizadas y sin vacías.</summary>
    public static List<string> EnPalabras(string? texto)
    {
        var sinTildes = new string((texto ?? "")
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        return sinTildes.ToLowerInvariant()
            .Split(_separadores, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    /// <summary>Lo mismo, vuelto a unir con un solo espacio.</summary>
    public static string EnFrase(string? texto) => string.Join(' ', EnPalabras(texto));
}

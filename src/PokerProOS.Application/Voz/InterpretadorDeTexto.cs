using System.Globalization;
using System.Text;

namespace PokerProOS.Application.Voz;

/// <summary>
/// Convierte el texto que reconoció el navegador en un <see cref="DictadoReconocido"/>.
///
/// Reemplaza a la gramática SRGS, y la diferencia que importa es que ESTE
/// puede rechazar. Una gramática está obligada a elegir la entrada más
/// parecida de su lista: ante "cuba" devolvía `cu` —la reina— con confianza
/// suficiente para pasar. Acá, si sobra un token que el vocabulario no
/// explica, se descarta la frase entera.
///
/// Es estricto a propósito: una orden se dicta sola, y ante la duda es mejor
/// no contestar que cambiar de tabla sin que lo hayan pedido.
/// </summary>
public sealed class InterpretadorDeTexto(IRegistroDeVocabulario vocabulario)
{
    public DictadoReconocido? Interpretar(string texto, float confianza)
    {
        var tokens = Normalizar(texto);
        if (tokens.Count == 0) return null;

        // null marca "ya consumido". Se busca de formas largas a cortas para
        // que "contra limp" gane sobre cualquier forma de una sola palabra
        // que empiece igual.
        var libres = new List<string?>(tokens);

        // Situaciones, spots y palos se consumen en UNA sola pasada, larga a
        // corta, mezclando las tres categorías. Si fueran pasadas separadas
        // por categoría, una forma corta de una categoría temprana (la
        // situación "be be contra min raise", 5 palabras) se comería el
        // prefijo de una forma larga de una categoría posterior (el spot
        // "be be contra min raise del boton", 7 palabras) y esa dejaría
        // "del boton" sueltos: la frase entera se rechazaría y el spot nunca
        // resolvería por su dicho canónico.
        var (formato, situacion, spot, palo) = ConsumirDichos(libres);

        // El stack va DESPUÉS de la pasada de dichos: si fuera antes, "be be"
        // de una frase de situación se lo llevaría el stack en vez de quedar
        // disponible para la situación/spot que lo necesita entero.
        var stack = ConsumirStack(libres);

        // Los rangos van ÚLTIMOS: en "nueve be be reina nueve suited" los dos
        // "nueve" son candidatos a rango, y si se consumieran antes que el
        // stack se comerían el "nueve" que en realidad es el número del stack.
        var rangos = ConsumirRangos(libres);

        // Sobró algo que el vocabulario no explica: no es una orden.
        if (libres.Any(t => t is not null)) return null;

        var hayMano = rangos.Count == 2;
        var hayContexto = formato is not null || situacion is not null
                          || spot is not null || stack is not null;
        if (!hayMano && !hayContexto) return null;

        // Un rango suelto es media mano: no alcanza para consultar, y como
        // contexto no significa nada.
        if (rangos.Count == 1) return null;

        return new DictadoReconocido(
            stack, spot, situacion, formato,
            hayMano ? rangos[0] : "",
            hayMano ? rangos[1] : "",
            hayMano ? palo : null,
            confianza,
            texto.Trim());
    }

    /// <summary>Minúsculas, sin tildes y partido en palabras.</summary>
    private static List<string> Normalizar(string texto)
    {
        var sinTildes = new string((texto ?? "")
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        return sinTildes.ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static List<string> Palabras(string dicho) =>
        dicho.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>
    /// Busca la primera forma de la categoría que aparezca completa y la marca
    /// consumida. De más larga a más corta: si "off" y "off suit" son las dos
    /// formas del mismo palo, ganar con la corta dejaría "suit" suelto y la
    /// frase entera se rechazaría.
    /// </summary>
    private static string? ConsumirForma(List<string?> libres, IReadOnlyList<FormasHabladas> formas)
    {
        var candidatas = formas
            .SelectMany(f => f.Dichos.Select(d => (f.Clave, Palabras: Palabras(d))))
            .OrderByDescending(c => c.Palabras.Count);

        foreach (var (clave, palabras) in candidatas)
        {
            var desde = Buscar(libres, palabras);
            if (desde < 0) continue;
            for (var i = 0; i < palabras.Count; i++) libres[desde + i] = null;
            return clave;
        }
        return null;
    }

    private enum CategoriaDeDicho { Formato, Situacion, Spot, Palo }

    /// <summary>
    /// Situaciones, spots y palos, todos juntos en una sola cola ordenada de
    /// más palabras a menos, sin importar la categoría. "De más larga a más
    /// corta" tiene que valer entre categorías y no solo dentro de cada una:
    /// si se consumiera categoría por categoría, una forma corta de una
    /// categoría que se prueba antes (una situación de 5 palabras) le podría
    /// ganar a una forma larga de una categoría posterior que la contiene
    /// como prefijo (un spot de 7 palabras que empieza igual), dejando el
    /// resto del spot suelto y la frase entera rechazada.
    /// </summary>
    private (string? Formato, string? Situacion, string? Spot, string? Palo) ConsumirDichos(
        List<string?> libres)
    {
        IEnumerable<(CategoriaDeDicho Categoria, string Clave, List<string> Palabras)> Formas(
            CategoriaDeDicho categoria, IReadOnlyList<FormasHabladas> formas) =>
            formas.SelectMany(f => f.Dichos.Select(d => (categoria, f.Clave, Palabras(d))));

        var candidatas = Formas(CategoriaDeDicho.Formato, vocabulario.Formatos)
            .Concat(Formas(CategoriaDeDicho.Situacion, vocabulario.Situaciones))
            .Concat(Formas(CategoriaDeDicho.Spot, vocabulario.Spots))
            .Concat(Formas(CategoriaDeDicho.Palo, vocabulario.Palos))
            .OrderByDescending(c => c.Palabras.Count);

        string? formato = null, situacion = null, spot = null, palo = null;

        foreach (var (categoria, clave, palabras) in candidatas)
        {
            // Cada categoría resuelve como mucho una vez: si ya se encontró
            // spot, un dicho de spot más corto no debe pisar al que ganó.
            var yaResuelta = categoria switch
            {
                CategoriaDeDicho.Formato => formato is not null,
                CategoriaDeDicho.Situacion => situacion is not null,
                CategoriaDeDicho.Spot => spot is not null,
                _ => palo is not null
            };
            if (yaResuelta) continue;

            var desde = Buscar(libres, palabras);
            if (desde < 0) continue;
            for (var i = 0; i < palabras.Count; i++) libres[desde + i] = null;

            switch (categoria)
            {
                case CategoriaDeDicho.Formato: formato = clave; break;
                case CategoriaDeDicho.Situacion: situacion = clave; break;
                case CategoriaDeDicho.Spot: spot = clave; break;
                default: palo = clave; break;
            }
        }

        return (formato, situacion, spot, palo);
    }

    /// <summary>
    /// Hasta dos rangos, en el orden en que aparecen en el vocabulario —no en
    /// el que se dictaron—, porque quien arma la mano ya los ordena de mayor
    /// a menor antes de componerla.
    /// </summary>
    private List<string> ConsumirRangos(List<string?> libres)
    {
        var encontrados = new List<string>();
        while (ConsumirForma(libres, vocabulario.Rangos) is { } clave)
        {
            encontrados.Add(clave);
            if (encontrados.Count == 2) break;
        }
        return encontrados;
    }

    /// <summary>
    /// Un número seguido de una palabra de stack ("nueve be be"). El número
    /// solo no alcanza: en "nueve ocho suited" los dos son rangos, y es la
    /// palabra la que convierte al primero en stack.
    /// </summary>
    private decimal? ConsumirStack(List<string?> libres)
    {
        foreach (var palabraDeStack in vocabulario.PalabrasDeStack
                     .OrderByDescending(p => Palabras(p).Count))
        {
            var palabras = Palabras(palabraDeStack);
            var desde = Buscar(libres, palabras);
            if (desde <= 0) continue;

            var numero = NumeroHablado.Interpretar(libres[desde - 1] ?? "");
            if (numero is null) continue;

            libres[desde - 1] = null;
            for (var i = 0; i < palabras.Count; i++) libres[desde + i] = null;
            return numero.Value;
        }
        return null;
    }

    /// <summary>Posición donde <paramref name="palabras"/> aparece entera y libre, o -1.</summary>
    private static int Buscar(List<string?> libres, List<string> palabras)
    {
        for (var i = 0; i + palabras.Count <= libres.Count; i++)
        {
            var coincide = true;
            for (var j = 0; j < palabras.Count && coincide; j++)
                coincide = libres[i + j] == palabras[j];
            if (coincide) return i;
        }
        return -1;
    }
}

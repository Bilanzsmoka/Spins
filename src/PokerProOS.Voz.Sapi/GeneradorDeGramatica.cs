using System.Globalization;
using System.Speech.Recognition;
using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;

namespace PokerProOS.Voz.Sapi;

/// <summary>
/// Construye la gramática restringida a partir del catálogo y los registros.
/// Nada de listas en código: al agregar una tabla de un stack nuevo, la voz
/// lo entiende sin tocar nada.
/// </summary>
public sealed class GeneradorDeGramatica(
    ICatalogoDeTablas catalogo,
    IRegistroDeVocabulario vocabulario,
    OpcionesDeVoz opciones)
{
    /// <summary>
    /// Dos formas de frase, no una: la <b>consulta</b> (con mano) y el
    /// <b>contexto</b> (sin mano). Sin la segunda hay que nombrar una mano en
    /// cada frase aunque solo se quiera cambiar de tabla, porque los rangos
    /// son obligatorios en la consulta.
    ///
    /// Se modelan como alternativas y no como una sola frase con todo
    /// opcional: una gramática donde nada es obligatorio matchea casi
    /// cualquier ruido y dispararía sola.
    /// </summary>
    public Grammar Construir()
    {
        var cultura = new CultureInfo(opciones.Cultura);
        var stacks = Stacks();

        var formas = new List<GrammarBuilder> { Consulta(cultura, stacks) };
        formas.AddRange(Contextos(cultura, stacks));

        return new Grammar(new Choices([.. formas])) { Name = "consulta-de-mano" };
    }

    /// <summary>La frase completa: contexto opcional y mano obligatoria.</summary>
    private GrammarBuilder Consulta(CultureInfo cultura, Choices? stacks)
    {
        var constructor = new GrammarBuilder { Culture = cultura };
        constructor.Append(new SemanticResultKey("situacion", Formas(vocabulario.Situaciones)), 0, 1);

        // SAPI rechaza en la compilación SRGS un <one-of> sin elementos, incluso
        // dentro de un rango opcional (0,1). Si el catálogo no cubre ningún
        // stack (p.ej. arranque sin datos), se omite el elemento entero en vez
        // de agregar un Choices vacío.
        if (stacks is not null)
        {
            constructor.Append(new SemanticResultKey("stack", stacks), 0, 1);
            constructor.Append(Choices(vocabulario.PalabrasDeStack), 0, 1);
        }

        constructor.Append(new SemanticResultKey("alta", Formas(vocabulario.Rangos)));
        constructor.Append(new SemanticResultKey("baja", Formas(vocabulario.Rangos)));
        constructor.Append(new SemanticResultKey("palo", Formas(vocabulario.Palos)), 0, 1);
        constructor.Append(new SemanticResultKey("spot", Formas(vocabulario.Spots)), 0, 1);
        return constructor;
    }

    /// <summary>
    /// Las órdenes de contexto, una pieza por frase: "heads up", "nueve be be",
    /// "contra limp". De a una y no combinadas a propósito: son frases cortas
    /// y dejarlas encadenarse las vuelve ambiguas entre sí, que es justo lo
    /// que degrada el reconocimiento.
    /// </summary>
    private IEnumerable<GrammarBuilder> Contextos(CultureInfo cultura, Choices? stacks)
    {
        var situacion = new GrammarBuilder { Culture = cultura };
        situacion.Append(new SemanticResultKey("situacion", Formas(vocabulario.Situaciones)));
        yield return situacion;

        var spot = new GrammarBuilder { Culture = cultura };
        spot.Append(new SemanticResultKey("spot", Formas(vocabulario.Spots)));
        yield return spot;

        if (stacks is null) yield break;

        // El número solo sería un disparo constante contra cualquier ruido;
        // la palabra ("be be", "blinds") es lo que lo vuelve una orden.
        var stack = new GrammarBuilder { Culture = cultura };
        stack.Append(new SemanticResultKey("stack", stacks));
        stack.Append(Choices(vocabulario.PalabrasDeStack));
        yield return stack;
    }

    /// <summary>
    /// Los números de stack salen de la cobertura real de las tablas: se toma
    /// el mínimo y el máximo entero que alguna tabla cubre. Null cuando el
    /// catálogo no tiene ninguna tabla cargada.
    /// </summary>
    private Choices? Stacks()
    {
        var rangos = catalogo.Situaciones
            .SelectMany(s => s.Stacks)
            .Select(t => t.Stack)
            .ToList();

        if (rangos.Count == 0) return null;

        var minimo = (int)Math.Floor(rangos.Min(r => r.MinBB));
        var maximo = (int)Math.Ceiling(rangos.Max(r => r.MaxBB));

        var valores = new Choices();
        for (var bb = minimo; bb <= maximo; bb++)
            valores.Add(new SemanticResultValue(
                bb.ToString(CultureInfo.InvariantCulture), bb));

        return valores;
    }

    private static Choices Formas(IReadOnlyList<FormasHabladas> formas)
    {
        // Choices no acepta SemanticResultValue[] en el constructor:
        // hay que instanciar vacio y usar Add en bucle.
        var opciones = new Choices();
        foreach (var forma in formas)
            foreach (var dicho in forma.Dichos)
                opciones.Add(new SemanticResultValue(dicho, forma.Clave));
        return opciones;
    }

    private static Choices Choices(IReadOnlyList<string> palabras)
    {
        var opciones = new Choices();
        foreach (var palabra in palabras) opciones.Add(palabra);
        return opciones;
    }
}

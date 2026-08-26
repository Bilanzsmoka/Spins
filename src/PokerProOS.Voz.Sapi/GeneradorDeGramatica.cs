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
    IRegistroDeVocabulario vocabulario)
{
    public Grammar Construir()
    {
        var cultura = new CultureInfo("es-ES");

        var constructor = new GrammarBuilder { Culture = cultura };
        constructor.Append(new SemanticResultKey("situacion", Formas(vocabulario.Situaciones)), 0, 1);
        constructor.Append(new SemanticResultKey("stack", Stacks()), 0, 1);
        constructor.Append(Choices(vocabulario.PalabrasDeStack), 0, 1);
        constructor.Append(new SemanticResultKey("alta", Formas(vocabulario.Rangos)));
        constructor.Append(new SemanticResultKey("baja", Formas(vocabulario.Rangos)));
        constructor.Append(new SemanticResultKey("palo", Formas(vocabulario.Palos)), 0, 1);
        constructor.Append(new SemanticResultKey("spot", Formas(vocabulario.Spots)), 0, 1);

        return new Grammar(constructor) { Name = "consulta-de-mano" };
    }

    /// <summary>
    /// Los números de stack salen de la cobertura real de las tablas: se toma
    /// el mínimo y el máximo entero que alguna tabla cubre.
    /// </summary>
    private Choices Stacks()
    {
        var rangos = catalogo.Situaciones
            .SelectMany(s => s.Stacks)
            .Select(t => t.Stack)
            .ToList();

        var opciones = new Choices();
        if (rangos.Count == 0) return opciones;

        var minimo = (int)Math.Floor(rangos.Min(r => r.MinBB));
        var maximo = (int)Math.Ceiling(rangos.Max(r => r.MaxBB));

        for (var bb = minimo; bb <= maximo; bb++)
            opciones.Add(new SemanticResultValue(
                bb.ToString(CultureInfo.InvariantCulture), bb));

        return opciones;
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

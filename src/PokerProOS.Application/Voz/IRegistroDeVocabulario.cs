namespace PokerProOS.Application.Voz;

/// <summary>El primer elemento de <see cref="Dichos"/> es la forma canónica: la que se usa al hablar de vuelta.</summary>
public record FormasHabladas(string Clave, IReadOnlyList<string> Dichos);

public interface IRegistroDeVocabulario
{
    IReadOnlyList<string> PalabrasDeStack { get; }
    IReadOnlyList<FormasHabladas> Rangos { get; }
    IReadOnlyList<FormasHabladas> Palos { get; }
    IReadOnlyList<FormasHabladas> Spots { get; }
    IReadOnlyList<FormasHabladas> Situaciones { get; }

    /// <summary>
    /// El formato de mesa ("HU", "3-max"). Es lo primero que se dicta al
    /// cambiar de tabla y lo unico que no se podia decir: el selector existia
    /// en pantalla pero la voz no tenia como nombrarlo.
    /// </summary>
    IReadOnlyList<FormasHabladas> Formatos { get; }

    /// <summary>
    /// Manos enteras, con clave de la matriz ("AKo"). Existe porque enseñar
    /// rangos sueltos no siempre alcanza: el navegador funde "as rey" en una
    /// palabra que no se puede partir en dos, y entonces no hay rango que
    /// enseñar. Arranca vacía; se llena sola desde la pantalla.
    ///
    /// Enseñar el rango es lo que generaliza —una forma nueva de "nueve"
    /// arregla todas las manos con un nueve—, así que esto es la excepción,
    /// no el camino principal.
    /// </summary>
    IReadOnlyList<FormasHabladas> Manos { get; }

    /// <summary>
    /// Cómo se nombra cada nivel del flujo: formato, situación, stack, spot y
    /// mano. Dicho al principio de la frase, el intérprete busca SOLO dentro
    /// de ese nivel.
    ///
    /// Existe porque el barrido libre mezcla categorías: "tres max" (el
    /// formato) se come el "tres" que era el rango, y "be be contra limp" (la
    /// situación) se come el "contra limp" que era el spot. Son 121 choques
    /// medidos, no una posibilidad teórica. Nombrar el nivel los elimina de
    /// raíz, porque ya no hay dos categorías compitiendo por las mismas
    /// palabras.
    ///
    /// Las claves son las de <see cref="NivelDeDictado"/>.
    /// </summary>
    IReadOnlyList<FormasHabladas> Niveles { get; }
}

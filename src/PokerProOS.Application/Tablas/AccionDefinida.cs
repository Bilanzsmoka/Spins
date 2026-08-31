namespace PokerProOS.Application.Tablas;

public record AccionDefinida(
    string Clave,
    string Etiqueta,
    string Color,
    string ColorTexto,
    int Orden,
    IReadOnlyList<string> Dichos,
    /// <summary>
    /// Cuánto compromete esta acción, para poder medir qué tan lejos quedó una
    /// respuesta de la correcta. Confundir dos tamaños de subida es un desliz;
    /// tirar donde había que empujar, no.
    ///
    /// Sale del archivo y no de la clave: deducir que RAISE_X4 pesa más que
    /// RAISE_X3 leyendo el número del identificador es exactamente lo que este
    /// proyecto no hace. Cero cuando el registro no la declara — y entonces
    /// ningún error es "cerca", que es el lado seguro.
    /// </summary>
    int Agresion = 0);

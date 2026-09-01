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
    int Agresion = 0,
    /// <summary>
    /// Cuántas ciegas pone esta acción. Sólo las subidas: FOLD y CHECK no
    /// ponen nada, CALL pone lo que haya delante y un all-in pone el stack.
    ///
    /// Está declarado y no deducido del nombre: leer el "3" de RAISE_X3 sería
    /// sacar un dato del identificador, que es lo que este proyecto no hace.
    /// Nulo cuando la acción no tiene un tamaño fijo.
    /// </summary>
    decimal? Tamano = null);

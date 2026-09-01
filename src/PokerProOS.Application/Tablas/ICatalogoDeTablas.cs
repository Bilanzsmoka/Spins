using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;

namespace PokerProOS.Application.Tablas;

/// <summary>
/// Qué ya pasó cuando este spot te toca.
///
/// Los spots de una tabla son una secuencia: primero decidís vos, y después
/// vienen los spots de "y si te resuben". Sin esto, la mesa de un 3-bet se
/// dibujaba igual que la de una primera decisión —sin tu subida delante— y se
/// leía como si nunca hubieras actuado.
///
/// Los montos de una subida van nulos porque dependen de la mano: se muestra
/// <b>que</b> subiste, no cuánto. Inventar un número sería peor.
/// </summary>
public record MesaDelSpot(
    string HeroeHizo,
    decimal? HeroePuso,
    IReadOnlyList<RivalEnLaMesa> Rivales);

public record SpotDeTabla(
    string Clave,
    string Etiqueta,
    IReadOnlyList<CeldaDeTabla> Celdas,
    /// <summary>
    /// El porqué escrito a mano: lo único de la ficha que ningún cálculo puede
    /// deducir de la tabla. Nulo si el spot no lo declara.
    /// </summary>
    string? Tip = null,
    /// <summary>Qué ya pasó. Nula en el spot donde decidís primero.</summary>
    MesaDelSpot? Mesa = null)
{
    private readonly Dictionary<string, string> _porMano =
        Celdas.ToDictionary(c => c.Mano, c => c.Accion, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> Conteos { get; } = Celdas
        .GroupBy(c => c.Accion, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, CeldaDeTabla> _celdaPorMano =
        Celdas.ToDictionary(c => c.Mano, c => c, StringComparer.OrdinalIgnoreCase);

    public string? AccionDe(string mano) => _porMano.GetValueOrDefault(mano);

    /// <summary>La celda completa, con su mix si lo tiene.</summary>
    public CeldaDeTabla? CeldaDe(string mano) => _celdaPorMano.GetValueOrDefault(mano);

    /// <summary>
    /// Si la mano está en el filo de su bloque: alguna vecina de la matriz
    /// tiene otra acción, o la propia celda es mixta —una mano mixta es un
    /// borde por definición, la tabla misma dice que ahí no hay respuesta
    /// única—.
    ///
    /// Vive acá y no en quien pregunta porque lo necesitan dos: el resolvedor,
    /// para avisar por voz, y el planificador, para elegir qué material nuevo
    /// enseña algo. Dos copias del cálculo se despegarían.
    /// </summary>
    public bool EnElBorde(string mano)
    {
        var accion = AccionDe(mano);
        if (accion is null) return false;
        if (CeldaDe(mano)?.EsMixta == true) return true;

        return MatrizDeManos.Vecinas(mano).Any(vecina =>
            !string.Equals(AccionDe(vecina), accion, StringComparison.OrdinalIgnoreCase));
    }
}

public record TablaDeStack(RangoDeStack Stack, IReadOnlyList<SpotDeTabla> Spots)
{
    public SpotDeTabla? Spot(string clave) =>
        Spots.FirstOrDefault(s => string.Equals(s.Clave, clave, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Un rival en la mesa: dónde está sentado, qué clase de jugador es y qué hizo
/// antes de que sea tu turno.
/// </summary>
/// <param name="Tipo">
/// El término del glosario —"Fish", "Reg"— del que salen su color y su figura.
/// Es lo que hace que el rival se reconozca sin leer.
/// </param>
/// <param name="Hizo">"limp", "min-raise", "all-in", "call", "fold", "por actuar".</param>
/// <param name="Puso">
/// Cuántas ciegas grandes tiene puestas delante. Nulo cuando fue all-in: ahí
/// puso su stack, que no es un número fijo. Va declarado y no calculado del
/// <paramref name="Hizo"/> porque un limp vale una ciega por definición del
/// juego, no por una regla que convenga tener escondida en el código.
/// </param>
/// <param name="VecesLaSubida">
/// Cuánto puso, medido en subidas tuyas: un 3-bet son 2,5 veces lo que subiste.
///
/// Va como múltiplo y no como monto porque el monto depende de cuánto subiste
/// vos, y eso cambia con cada mano. Nulo cuando lo que puso es un número fijo
/// o cuando no se sabe.
/// </param>
public record RivalEnLaMesa(
    string Posicion, string Tipo, string Hizo,
    decimal? Puso = null, decimal? VecesLaSubida = null);

/// <summary>
/// Cómo se ve la mesa cuando te toca decidir: dónde estás sentado, quién más
/// hay y qué hicieron.
///
/// Se <b>declara</b> en el archivo, no se deduce de la clave de la situación.
/// Deducir "BB vs BTN limp" de un identificador es exactamente lo que este
/// proyecto no hace, y acá importa más que en ningún lado: una mesa mal
/// dibujada enseña una mano equivocada.
/// </summary>
/// <param name="Boton">
/// Qué silla tiene el botón. En 3-max es el BTN; en heads-up lo tiene la ciega
/// chica, porque ahí el botón y la SB son el mismo jugador.
/// </param>
/// <param name="PusoElHeroe">Tu ciega, ya en el pozo antes de decidir.</param>
public record MesaDeSituacion(
    string Heroe,
    decimal CiegaChica,
    decimal CiegaGrande,
    IReadOnlyList<RivalEnLaMesa> Rivales,
    string Boton = "",
    decimal PusoElHeroe = 0);

/// <summary>
/// El formato de mesa al que pertenece la situación ("HU", "3-max"). Lo
/// declara el archivo, no lo deduce el código de la clave: agregar un formato
/// nuevo tiene que ser dejar un JSON, igual que agregar una tabla.
/// </summary>
public record SituacionDeTabla(
    string Clave,
    string Etiqueta,
    string Formato,
    IReadOnlyList<TablaDeStack> Stacks,
    /// <summary>
    /// Qué es esta situación, en castellano: qué pasó en la mesa y dónde
    /// estás parado. Escrita a mano, como el <c>tip</c> del spot — ningún
    /// cálculo puede deducir qué significa "BB vs 3-way limp".
    ///
    /// Es descripción, no estrategia: la estrategia vive en el tip de cada
    /// spot, que depende del stack. Nula si el archivo no la declara.
    /// </summary>
    string? Explicacion = null,
    /// <summary>Cómo se ve la mesa. Nula si el archivo no la declara.</summary>
    MesaDeSituacion? Mesa = null);

public interface ICatalogoDeTablas
{
    IReadOnlyList<SituacionDeTabla> Situaciones { get; }
    IReadOnlyList<ProblemaDeTabla> Problemas { get; }
    SituacionDeTabla? Situacion(string clave);
    TablaDeStack? StackQueCubre(string situacion, decimal bb);
    TablaDeStack? StackPorClave(string situacion, string claveStack);
    SpotDeTabla? Spot(string situacion, string claveStack, string claveSpot);
}

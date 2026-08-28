namespace PokerProOS.Domain.Entrenador;

/// <summary>
/// Lo que se sabe de UNA casilla: usuario + situación + stack + spot + mano.
///
/// La unidad no es la mano sino la casilla, porque K2s en BB vs open shove a
/// 11bb es una cosa distinta de K2s en el mismo spot a 20bb: la respuesta
/// correcta cambia entre las dos, y aprender la tabla ES aprender dónde está
/// ese corte.
/// </summary>
public class ProgresoDeCasilla
{
    public int Id { get; set; }

    /// <summary>
    /// Va en la clave desde el día uno aunque todavía no haya login. El día
    /// que lo haya, el progreso ya está separado por persona y no hay datos
    /// que migrar.
    /// </summary>
    public int UsuarioId { get; set; }

    public string Situacion { get; set; } = "";
    public string ClaveDeStack { get; set; } = "";
    public string Spot { get; set; } = "";
    public string Mano { get; set; } = "";

    /// <summary>Cuántas veces seguidas se acertó. Un fallo la vuelve a cero.</summary>
    public int AciertosSeguidos { get; set; }

    /// <summary>Cuántos días duró el descanso que se acaba de conceder.</summary>
    public int IntervaloEnDias { get; set; }

    /// <summary>El día a partir del cual la casilla vuelve a entrar en una tanda.</summary>
    public DateOnly Vence { get; set; }

    public DateTime ActualizadaEn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// La identidad de la casilla como texto, para juntarlas en un conjunto.
    /// Vive acá y no en quien la use para que el planificador y el repositorio
    /// no puedan armarla distinto: si lo hicieran, material ya estudiado
    /// reaparecería como nuevo.
    /// </summary>
    public static string Clave(string situacion, string claveDeStack, string spot, string mano)
        => $"{situacion}|{claveDeStack}|{spot}|{mano}";

    public string ClaveDeCasilla() => Clave(Situacion, ClaveDeStack, Spot, Mano);
}

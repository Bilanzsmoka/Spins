namespace PokerProOS.Domain.Diario;

/// <summary>
/// Lo que se marcó de un hábito un día. Tabla hija en vez de columnas para
/// que agregar o quitar hábitos no toque el esquema.
/// </summary>
public class MarcaDeHabito
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public string Clave { get; set; } = string.Empty;

    /// <summary>
    /// Binarios: 1 hecho, -1 no hecho, 0 sin marcar. Numéricos: el valor.
    /// </summary>
    public int Valor { get; set; }
}

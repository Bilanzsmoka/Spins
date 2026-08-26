namespace PokerProOS.Domain.Bitacora;

public class ConsultaDeVoz
{
    public int Id { get; set; }
    public string Situacion { get; set; } = "";
    public string ClaveDeStack { get; set; } = "";
    public string Spot { get; set; } = "";
    public string Mano { get; set; } = "";
    public string Accion { get; set; } = "";
    public string Respuesta { get; set; } = "";
    public bool Resuelta { get; set; }
    public string TextoCrudo { get; set; } = "";
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
}

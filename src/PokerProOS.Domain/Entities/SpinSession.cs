namespace PokerProOS.Domain.Entities;

public class SpinSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? RoomId { get; set; }
    public string Stake { get; set; } = string.Empty;
    public decimal BuyIn { get; set; }
    public int Tournaments { get; set; }
    public int FreeTournaments { get; set; }
    public decimal PrizeTotal { get; set; }
    public decimal NetResult { get; set; }
    public decimal Rakeback { get; set; }
    public decimal PromoValue { get; set; }
    public decimal ChipEvTotal { get; set; }
    public int Minutes { get; set; }
    public string? Notes { get; set; }
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
}

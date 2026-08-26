namespace PokerProOS.Domain.Entities;

public class SpinTournament
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Site { get; set; } = "GG";
    public string TournamentId { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public decimal BuyIn { get; set; }
    public int? HeroRank { get; set; }
    public int Hands { get; set; }
    public int HeroAllins { get; set; }
    public int HeroCallsAllin { get; set; }
    public int HeroRaises { get; set; }
    public int HeroLimps { get; set; }
    public int HeroPreflopFolds { get; set; }
    public DateTime? FirstPlayedAt { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    public string RawText { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

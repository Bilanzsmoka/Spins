namespace PokerProOS.Domain.Entities;

public class ChartStrategyCell
{
    public int Id { get; set; }
    public string SituationKey { get; set; } = string.Empty;
    public string SituationLabel { get; set; } = string.Empty;
    public string StackKey { get; set; } = string.Empty;
    public decimal MinBB { get; set; }
    public decimal MaxBB { get; set; }
    public string SpotKey { get; set; } = string.Empty;
    public string SpotLabel { get; set; } = string.Empty;
    public string HandLabel { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Source { get; set; } = "json-import";
    public string Version { get; set; } = "v1";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

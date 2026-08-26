namespace PokerProOS.Domain.Entities;

public class TrainerAttempt
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Pack { get; set; } = "checkcheck-fish";
    public string Format { get; set; } = "all";
    public string Spot { get; set; } = string.Empty;
    public int StackBB { get; set; }
    public string Villain { get; set; } = "base";
    public string HandLabel { get; set; } = string.Empty;
    public string ExpectedAction { get; set; } = string.Empty;
    public string ChosenAction { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public decimal Score { get; set; }
    public decimal Adjustment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

namespace PokerProOS.Application.Charts.Validators;

public class ChartValidator
{
    private static readonly HashSet<string> ValidActions = new() { "ALL-IN", "CALL", "FOLD", "RAISE_X2" };
    private const int HandsPerSpot = 169;

    public ValidationResult Validate(string jsonContent)
    {
        var errors = new List<string>();
        return new ValidationResult(errors);
    }
}

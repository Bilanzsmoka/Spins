using System.Text.RegularExpressions;

namespace PokerProOS.Domain.ValueObjects;

public partial record HandLabel
{
    public string Value { get; }

    private static readonly Regex ValidHandPattern = HandLabelRegex();

    private HandLabel(string value)
    {
        Value = value;
    }

    public static HandLabel Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Hand label cannot be empty.");

        var normalized = raw.Trim().ToUpperInvariant();

        if (!ValidHandPattern.IsMatch(normalized))
            throw new ArgumentException($"Invalid hand label format: {raw}");

        return new HandLabel(normalized);
    }

    public static bool IsValid(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return ValidHandPattern.IsMatch(raw.Trim().ToUpperInvariant());
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^(2-9|T|J|Q|K|A){2}[so]?$")]
    private static partial Regex HandLabelRegex();
}

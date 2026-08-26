namespace PokerProOS.Domain.ValueObjects;

public record StackRange
{
    public string Key { get; }
    public decimal MinBB { get; }
    public decimal MaxBB { get; }

    public StackRange(string key, decimal minBB, decimal maxBB)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Stack key cannot be empty.");
        if (minBB < 0 || maxBB < 0)
            throw new ArgumentException("BB values cannot be negative.");
        if (minBB > maxBB)
            throw new ArgumentException("MinBB cannot be greater than MaxBB.");

        Key = key;
        MinBB = minBB;
        MaxBB = maxBB;
    }

    public bool Covers(decimal bb) => bb >= MinBB && bb <= MaxBB;
}

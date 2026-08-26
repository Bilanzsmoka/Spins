namespace PokerProOS.Application.Charts.DTOs;

public record ImportResult
{
    public int TotalRows { get; init; }
    public int FilesProcessed { get; init; }
    public List<string> Errors { get; init; } = new();
    public bool Success => Errors.Count == 0;
}

using System.Text.Json;
using Microsoft.Extensions.Logging;
using PokerProOS.Application.Charts.DTOs;
using PokerProOS.Application.Charts.Interfaces;
using PokerProOS.Domain.Entities;

namespace PokerProOS.Infrastructure.Services;

public class ChartImportService
{
    private readonly IChartRepository _repo;
    private readonly ILogger<ChartImportService> _logger;

    public ChartImportService(IChartRepository repo, ILogger<ChartImportService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<ImportResult> ImportFromDirectoryAsync(string directory)
    {
        if (!Directory.Exists(directory))
            return new ImportResult { Errors = new List<string> { $"Directory not found: {directory}" } };

        var jsonFiles = Directory.GetFiles(directory, "*.json");
        var allCells = new List<ChartStrategyCell>();
        var errors = new List<string>();
        var allPossibleHands = GenerateAllHands();

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("situation", out var situation))
                {
                    errors.Add($"{Path.GetFileName(file)}: missing 'situation'");
                    continue;
                }

                var situationKey = situation.GetProperty("key").GetString() ?? "";
                var situationLabel = situation.GetProperty("label").GetString() ?? "";

                if (!root.TryGetProperty("stacks", out var stacks))
                {
                    errors.Add($"{Path.GetFileName(file)}: missing 'stacks'");
                    continue;
                }

                foreach (var stack in stacks.EnumerateArray())
                {
                    var stackKey = stack.GetProperty("key").GetString() ?? "";
                    var minBB = stack.GetProperty("minBB").GetDecimal();
                    var maxBB = stack.GetProperty("maxBB").GetDecimal();

                    if (!stack.TryGetProperty("spots", out var spots)) continue;

                    foreach (var spot in spots.EnumerateArray())
                    {
                        var spotKey = spot.GetProperty("key").GetString() ?? "";
                        var spotLabel = spot.GetProperty("label").GetString() ?? "";

                        if (!spot.TryGetProperty("actions", out var actions)) continue;

                        var assignedHands = new Dictionary<string, string>();
                        var restAction = (string?)null;

                        foreach (var actionProp in actions.EnumerateObject())
                        {
                            var actionName = actionProp.Name;

                            if (actionProp.Value.ValueKind == JsonValueKind.String &&
                                actionProp.Value.GetString() == "REST")
                            {
                                restAction = actionName;
                                continue;
                            }

                            if (actionProp.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var hand in actionProp.Value.EnumerateArray())
                                {
                                    var handLabel = hand.GetString();
                                    if (handLabel != null)
                                        assignedHands[handLabel] = actionName;
                                }
                            }
                        }

                        if (restAction != null)
                        {
                            foreach (var hand in allPossibleHands)
                            {
                                if (!assignedHands.ContainsKey(hand))
                                    assignedHands[hand] = restAction;
                            }
                        }

                        foreach (var kvp in assignedHands)
                        {
                            allCells.Add(new ChartStrategyCell
                            {
                                SituationKey = situationKey,
                                SituationLabel = situationLabel,
                                StackKey = stackKey,
                                MinBB = minBB,
                                MaxBB = maxBB,
                                SpotKey = spotKey,
                                SpotLabel = spotLabel,
                                HandLabel = kvp.Key,
                                Action = kvp.Value,
                                Source = "json-import",
                                Version = "v1",
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        if (allCells.Count > 0)
        {
            var grouped = allCells.GroupBy(c => new { c.SituationKey, c.StackKey });
            foreach (var group in grouped)
            {
                await _repo.DeleteByStackAsync(group.Key.SituationKey, group.Key.StackKey);
                await _repo.ImportAsync(group.ToList());
            }
        }

        _logger.LogInformation("Imported {Count} cells from {Files} files", allCells.Count, jsonFiles.Length);

        return new ImportResult
        {
            TotalRows = allCells.Count,
            FilesProcessed = jsonFiles.Length,
            Errors = errors
        };
    }

    private static List<string> GenerateAllHands()
    {
        var ranks = new[] { "A", "K", "Q", "J", "T", "9", "8", "7", "6", "5", "4", "3", "2" };
        var hands = new List<string>();
        for (int i = 0; i < ranks.Length; i++)
        {
            for (int j = i; j < ranks.Length; j++)
            {
                if (i == j)
                    hands.Add($"{ranks[i]}{ranks[j]}");
                else
                {
                    hands.Add($"{ranks[i]}{ranks[j]}s");
                    hands.Add($"{ranks[i]}{ranks[j]}o");
                }
            }
        }
        return hands;
    }
}

namespace PokerProOS.Application.Charts.Queries;

public record GetChartByStackQuery(
    string SituationKey,
    string StackKey,
    string? SpotKey = null
);

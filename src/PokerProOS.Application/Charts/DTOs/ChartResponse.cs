namespace PokerProOS.Application.Charts.DTOs;

public record ChartResponse(
    string SituationKey,
    string SituationLabel,
    string StackKey,
    List<SpotResponse> Spots
);

public record SpotResponse(
    string SpotKey,
    string SpotLabel,
    List<HandAction> Hands,
    Dictionary<string, int> ActionCounts,
    int Total
);

public record HandAction(
    string HandLabel,
    string Action
);

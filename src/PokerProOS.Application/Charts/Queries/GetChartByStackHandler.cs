using PokerProOS.Application.Charts.DTOs;
using PokerProOS.Application.Charts.Interfaces;

namespace PokerProOS.Application.Charts.Queries;

public class GetChartByStackHandler
{
    private readonly IChartRepository _repo;

    public GetChartByStackHandler(IChartRepository repo) => _repo = repo;

    public async Task<ChartResponse?> Handle(GetChartByStackQuery query)
    {
        var cells = string.IsNullOrEmpty(query.SpotKey)
            ? await _repo.GetByStackAsync(query.SituationKey, query.StackKey)
            : await _repo.GetBySpotAsync(query.SituationKey, query.StackKey, query.SpotKey);

        if (cells.Count == 0) return null;

        var spots = cells
            .GroupBy(c => new { c.SpotKey, c.SpotLabel })
            .Select(g => new SpotResponse(
                g.Key.SpotKey,
                g.Key.SpotLabel,
                g.Select(c => new HandAction(c.HandLabel, c.Action)).ToList(),
                g.GroupBy(c => c.Action).ToDictionary(a => a.Key, a => a.Count()),
                g.Count()
            ))
            .ToList();

        return new ChartResponse(
            query.SituationKey,
            cells.First().SituationLabel,
            query.StackKey,
            spots
        );
    }
}

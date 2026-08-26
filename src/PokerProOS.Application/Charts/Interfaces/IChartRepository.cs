using PokerProOS.Domain.Entities;

namespace PokerProOS.Application.Charts.Interfaces;

public interface IChartRepository
{
    Task<List<ChartStrategyCell>> GetByStackAsync(string situationKey, string stackKey);
    Task<List<ChartStrategyCell>> GetBySpotAsync(string situationKey, string stackKey, string spotKey);
    Task ImportAsync(List<ChartStrategyCell> cells);
    Task<int> GetCountAsync(string situationKey, string stackKey, string spotKey);
    Task DeleteByStackAsync(string situationKey, string stackKey);
}

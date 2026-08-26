using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Charts.Interfaces;
using PokerProOS.Domain.Entities;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Infrastructure.Repositories;

public class ChartStrategyRepository : IChartRepository
{
    private readonly PokerProOSDbContext _context;

    public ChartStrategyRepository(PokerProOSDbContext context) => _context = context;

    public async Task<List<ChartStrategyCell>> GetByStackAsync(string situationKey, string stackKey)
    {
        return await _context.ChartStrategyCells
            .Where(c => c.SituationKey == situationKey && c.StackKey == stackKey)
            .OrderBy(c => c.SpotKey).ThenBy(c => c.HandLabel)
            .ToListAsync();
    }

    public async Task<List<ChartStrategyCell>> GetBySpotAsync(string situationKey, string stackKey, string spotKey)
    {
        return await _context.ChartStrategyCells
            .Where(c => c.SituationKey == situationKey && c.StackKey == stackKey && c.SpotKey == spotKey)
            .OrderBy(c => c.HandLabel)
            .ToListAsync();
    }

    public async Task ImportAsync(List<ChartStrategyCell> cells)
    {
        _context.ChartStrategyCells.AddRange(cells);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetCountAsync(string situationKey, string stackKey, string spotKey)
    {
        return await _context.ChartStrategyCells
            .CountAsync(c => c.SituationKey == situationKey && c.StackKey == stackKey && c.SpotKey == spotKey);
    }

    public async Task DeleteByStackAsync(string situationKey, string stackKey)
    {
        var cells = await _context.ChartStrategyCells
            .Where(c => c.SituationKey == situationKey && c.StackKey == stackKey)
            .ToListAsync();
        _context.ChartStrategyCells.RemoveRange(cells);
        await _context.SaveChangesAsync();
    }
}

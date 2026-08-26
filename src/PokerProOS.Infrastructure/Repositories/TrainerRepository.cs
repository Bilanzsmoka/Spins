using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Trainer.Interfaces;
using PokerProOS.Domain.Entities;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Infrastructure.Repositories;

public class TrainerRepository : ITrainerRepository
{
    private readonly PokerProOSDbContext _context;

    public TrainerRepository(PokerProOSDbContext context) => _context = context;

    public async Task<TrainerAttempt> SaveAttemptAsync(TrainerAttempt attempt)
    {
        _context.TrainerAttempts.Add(attempt);
        await _context.SaveChangesAsync();
        return attempt;
    }

    public async Task<List<TrainerAttempt>> GetAttemptsAsync(int userId, string? spot, int? stackBB)
    {
        var query = _context.TrainerAttempts.Where(t => t.UserId == userId);

        if (!string.IsNullOrEmpty(spot))
            query = query.Where(t => t.Spot == spot);

        if (stackBB.HasValue)
            query = query.Where(t => t.StackBB == stackBB.Value);

        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public async Task<Dictionary<string, decimal>> GetStatsAsync(int userId)
    {
        return await _context.TrainerAttempts
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Spot)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Average(t => t.IsCorrect ? 1m : 0m) * 100m
            );
    }
}

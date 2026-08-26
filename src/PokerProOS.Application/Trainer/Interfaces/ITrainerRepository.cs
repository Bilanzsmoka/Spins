using PokerProOS.Domain.Entities;

namespace PokerProOS.Application.Trainer.Interfaces;

public interface ITrainerRepository
{
    Task<TrainerAttempt> SaveAttemptAsync(TrainerAttempt attempt);
    Task<List<TrainerAttempt>> GetAttemptsAsync(int userId, string? spot, int? stackBB);
    Task<Dictionary<string, decimal>> GetStatsAsync(int userId);
}

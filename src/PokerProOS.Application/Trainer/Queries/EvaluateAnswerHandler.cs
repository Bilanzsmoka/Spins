using PokerProOS.Application.Trainer.Interfaces;
using PokerProOS.Domain.Entities;

namespace PokerProOS.Application.Trainer.Queries;

public class EvaluateAnswerHandler
{
    private readonly ITrainerRepository _repo;
    private readonly Charts.Interfaces.IChartRepository _chartRepo;

    public EvaluateAnswerHandler(ITrainerRepository repo, Charts.Interfaces.IChartRepository chartRepo)
    {
        _repo = repo;
        _chartRepo = chartRepo;
    }

    public async Task<TrainerAttempt> Handle(EvaluateAnswerQuery query)
    {
        var cells = await _chartRepo.GetBySpotAsync("HU_SB_OR_FISH", $"{query.StackBB}bb", query.Spot);
        var expected = cells.FirstOrDefault(c => c.HandLabel == query.HandLabel)?.Action ?? "UNKNOWN";

        var attempt = new TrainerAttempt
        {
            UserId = query.UserId,
            Pack = query.Pack,
            Format = query.Format,
            Spot = query.Spot,
            StackBB = query.StackBB,
            Villain = query.Villain,
            HandLabel = query.HandLabel,
            ExpectedAction = expected,
            ChosenAction = query.ChosenAction,
            IsCorrect = string.Equals(expected, query.ChosenAction, StringComparison.OrdinalIgnoreCase),
            Score = string.Equals(expected, query.ChosenAction, StringComparison.OrdinalIgnoreCase) ? 1m : 0m,
            CreatedAt = DateTime.UtcNow
        };

        return await _repo.SaveAttemptAsync(attempt);
    }
}

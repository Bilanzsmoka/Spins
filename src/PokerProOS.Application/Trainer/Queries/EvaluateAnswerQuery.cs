namespace PokerProOS.Application.Trainer.Queries;

public record EvaluateAnswerQuery(
    string HandLabel,
    string ChosenAction,
    int UserId,
    string Spot,
    int StackBB,
    string Pack = "checkcheck-fish",
    string Format = "all",
    string Villain = "base"
);

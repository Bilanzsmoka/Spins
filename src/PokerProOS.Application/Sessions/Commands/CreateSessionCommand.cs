namespace PokerProOS.Application.Sessions.Commands;

public record CreateSessionCommand(
    int UserId,
    int? RoomId,
    string Stake,
    decimal BuyIn,
    int Tournaments,
    int FreeTournaments,
    decimal PrizeTotal,
    decimal NetResult,
    decimal Rakeback,
    decimal PromoValue,
    decimal ChipEvTotal,
    int Minutes,
    string? Notes = null
);

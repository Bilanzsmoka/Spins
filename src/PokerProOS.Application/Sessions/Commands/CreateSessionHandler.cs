using PokerProOS.Application.Sessions.Interfaces;
using PokerProOS.Domain.Entities;

namespace PokerProOS.Application.Sessions.Commands;

public class CreateSessionHandler
{
    private readonly ISessionRepository _repo;
    public CreateSessionHandler(ISessionRepository repo) => _repo = repo;

    public async Task<SpinSession> Handle(CreateSessionCommand cmd)
    {
        var session = new SpinSession
        {
            UserId = cmd.UserId,
            RoomId = cmd.RoomId,
            Stake = cmd.Stake,
            BuyIn = cmd.BuyIn,
            Tournaments = cmd.Tournaments,
            FreeTournaments = cmd.FreeTournaments,
            PrizeTotal = cmd.PrizeTotal,
            NetResult = cmd.NetResult,
            Rakeback = cmd.Rakeback,
            PromoValue = cmd.PromoValue,
            ChipEvTotal = cmd.ChipEvTotal,
            Minutes = cmd.Minutes,
            Notes = cmd.Notes,
            PlayedAt = DateTime.UtcNow
        };
        return await _repo.CreateAsync(session);
    }
}

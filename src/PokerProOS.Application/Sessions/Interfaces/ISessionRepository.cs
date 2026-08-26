using PokerProOS.Domain.Entities;

namespace PokerProOS.Application.Sessions.Interfaces;

public interface ISessionRepository
{
    Task<SpinSession?> GetByIdAsync(int id);
    Task<List<SpinSession>> GetAllAsync(int userId);
    Task<SpinSession> CreateAsync(SpinSession session);
    Task<SpinSession> UpdateAsync(SpinSession session);
    Task DeleteAsync(int id);
}

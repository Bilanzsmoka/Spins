using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Sessions.Interfaces;
using PokerProOS.Domain.Entities;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Infrastructure.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly PokerProOSDbContext _context;

    public SessionRepository(PokerProOSDbContext context) => _context = context;

    public async Task<SpinSession?> GetByIdAsync(int id)
    {
        return await _context.SpinSessions.FindAsync(id);
    }

    public async Task<List<SpinSession>> GetAllAsync(int userId)
    {
        return await _context.SpinSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.PlayedAt)
            .ToListAsync();
    }

    public async Task<SpinSession> CreateAsync(SpinSession session)
    {
        _context.SpinSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<SpinSession> UpdateAsync(SpinSession session)
    {
        _context.SpinSessions.Update(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task DeleteAsync(int id)
    {
        var session = await _context.SpinSessions.FindAsync(id);
        if (session != null)
        {
            _context.SpinSessions.Remove(session);
            await _context.SaveChangesAsync();
        }
    }
}

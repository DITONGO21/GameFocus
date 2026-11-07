using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Infrastructure.Database;

namespace GameFocus.Infrastructure.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly GameFocusDbContext _context;

    public SessionRepository(GameFocusDbContext context)
    {
        _context = context;
    }

    public async Task<GameSession?> GetByIdAsync(int id)
    {
        return await _context.Sessions.FindAsync(id);
    }

    public async Task<IEnumerable<GameSession>> GetAllAsync()
    {
        return await _context.Sessions.ToListAsync();
    }

    public async Task<IEnumerable<GameSession>> GetByGameIdAsync(int gameId)
    {
        return await _context.Sessions
            .Where(s => s.GameId == gameId)
            .OrderByDescending(s => s.StartTimeUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<GameSession>> GetSessionsInDateRangeAsync(DateTime start, DateTime end)
    {
        return await _context.Sessions
            .Where(s => s.StartTimeUtc >= start && s.StartTimeUtc <= end)
            .Include(s => s.Game)
            .OrderByDescending(s => s.StartTimeUtc)
            .ToListAsync();
    }

    public async Task<GameSession?> GetActiveSessionAsync()
    {
        // No tracking: the dashboard reads this on a long-lived context, so it must see new sessions.
        return await _context.Sessions
            .AsNoTracking()
            .Include(s => s.Game)
            .Where(s => s.EndTimeUtc == null && s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.StartTimeUtc)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(GameSession session)
    {
        await _context.Sessions.AddAsync(session);
    }

    public void Update(GameSession session)
    {
        _context.Sessions.Update(session);
    }
}

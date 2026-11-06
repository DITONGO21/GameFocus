using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Infrastructure.Database;

namespace GameFocus.Infrastructure.Repositories;

public class SessionInsightRepository : ISessionInsightRepository
{
    private readonly GameFocusDbContext _context;

    public SessionInsightRepository(GameFocusDbContext context)
    {
        _context = context;
    }

    public async Task<SessionIntent?> GetIntentBySessionIdAsync(int sessionId)
    {
        return await _context.SessionIntents
            .FirstOrDefaultAsync(i => i.SessionId == sessionId);
    }

    public async Task<SessionFeedback?> GetFeedbackBySessionIdAsync(int sessionId)
    {
        return await _context.SessionFeedbacks
            .FirstOrDefaultAsync(f => f.SessionId == sessionId);
    }

    public async Task<IEnumerable<SessionIntent>> GetIntentsByGameIdAsync(int gameId)
    {
        return await _context.SessionIntents
            .Include(i => i.Session)
            .Where(i => i.Session.GameId == gameId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<SessionFeedback>> GetFeedbacksByGameIdAsync(int gameId)
    {
        return await _context.SessionFeedbacks
            .Include(f => f.Session)
            .Where(f => f.Session.GameId == gameId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<SessionFeedback>> GetRecentFeedbacksAsync(int count)
    {
        return await _context.SessionFeedbacks
            .Include(f => f.Session)
            .ThenInclude(s => s.Game)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Take(count)
            .ToListAsync();
    }

    public async Task AddIntentAsync(SessionIntent intent)
    {
        await _context.SessionIntents.AddAsync(intent);
    }

    public async Task AddFeedbackAsync(SessionFeedback feedback)
    {
        await _context.SessionFeedbacks.AddAsync(feedback);
    }
}

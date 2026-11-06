using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Infrastructure.Database;

namespace GameFocus.Infrastructure.Repositories;

public class GoalRepository : IGoalRepository
{
    private readonly GameFocusDbContext _context;

    public GoalRepository(GameFocusDbContext context)
    {
        _context = context;
    }

    public async Task<Goal?> GetByIdAsync(int id)
    {
        return await _context.Goals.FindAsync(id);
    }

    public async Task<IEnumerable<Goal>> GetByGameIdAsync(int gameId)
    {
        return await _context.Goals
            .Where(g => g.GameId == gameId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Goal>> GetActiveGoalsAsync()
    {
        // No tracking: the Goals page keeps a long-lived context, so tracked goals would hide
        // progress that GoalService saves from a session scope.
        return await _context.Goals
            .AsNoTracking()
            .Where(g => !g.IsCompleted)
            .Include(g => g.Game)
            .ToListAsync();
    }

    public async Task AddAsync(Goal goal)
    {
        await _context.Goals.AddAsync(goal);
    }

    public void Update(Goal goal)
    {
        var tracked = FindTracked(goal);
        if (tracked != null && !ReferenceEquals(tracked, goal))
        {
            // Another instance of this goal is already tracked: copy the new values onto it.
            _context.Entry(tracked).CurrentValues.SetValues(goal);
            return;
        }

        // Mark only the goal as modified. Goals read without tracking carry a Game navigation,
        // and Context.Goals.Update would mark that game as modified too and overwrite its row.
        _context.Entry(goal).State = EntityState.Modified;
    }

    public void Delete(Goal goal)
    {
        _context.Goals.Remove(FindTracked(goal) ?? goal);
    }

    // Goals are read without tracking, so the context may already track another instance with the same key.
    private Goal? FindTracked(Goal goal) =>
        _context.Goals.Local.FirstOrDefault(g => g.Id == goal.Id);
}

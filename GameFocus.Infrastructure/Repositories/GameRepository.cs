using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Infrastructure.Database;

namespace GameFocus.Infrastructure.Repositories;

public class GameRepository : IGameRepository
{
    private readonly GameFocusDbContext _context;

    public GameRepository(GameFocusDbContext context)
    {
        _context = context;
    }

    public async Task<Game?> GetByIdAsync(int id)
    {
        return await _context.Games
            .Include(g => g.Goals)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<IEnumerable<Game>> GetAllAsync()
    {
        // No tracking: the library keeps a long-lived context, so tracked entities would
        // hide status changes that the session coordinator saves from its own scope.
        return await _context.Games.AsNoTracking().ToListAsync();
    }

    public async Task<Game?> GetByExternalIdAsync(GameSourceType source, string externalId)
    {
        // No tracking: lookups run on the long-lived library context, so tracked games could be stale.
        return await _context.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Source == source && g.ExternalId == externalId);
    }

    public async Task<Game?> GetBySteamIdAsync(long steamId)
    {
        return await _context.Games.FirstOrDefaultAsync(g => g.SteamAppId == steamId);
    }

    public async Task AddAsync(Game game)
    {
        await _context.Games.AddAsync(game);
    }

    public void Update(Game game)
    {
        // Games are read without tracking, so the context may already track another instance with the same key.
        var tracked = _context.Games.Local.FirstOrDefault(g => g.Id == game.Id);
        if (tracked != null && !ReferenceEquals(tracked, game))
        {
            _context.Entry(tracked).CurrentValues.SetValues(game);
            return;
        }

        _context.Entry(game).State = EntityState.Modified;
    }

    public void Delete(Game game)
    {
        // Remove by key: attaching the whole graph of an untracked game can conflict with an instance
        // of the same game that the context already tracks. Cascades are handled by the database.
        var tracked = _context.Games.Local.FirstOrDefault(g => g.Id == game.Id);
        _context.Games.Remove(tracked ?? new Game { Id = game.Id });
    }
}

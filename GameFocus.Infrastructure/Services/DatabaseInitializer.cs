using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using GameFocus.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GameFocus.Infrastructure.Services;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly GameFocusDbContext _context;

    public DatabaseInitializer(GameFocusDbContext context)
    {
        _context = context;
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        await _context.Database.MigrateAsync();
    }
}

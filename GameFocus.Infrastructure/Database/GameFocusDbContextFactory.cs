using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace GameFocus.Infrastructure.Database;

public class GameFocusDbContextFactory : IDesignTimeDbContextFactory<GameFocusDbContext>
{
    public GameFocusDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GameFocusDbContext>();
        // Using a local sqlite file for design-time migrations
        var dbPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "GameFocus", "gamefocus.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new GameFocusDbContext(optionsBuilder.Options);
    }
}

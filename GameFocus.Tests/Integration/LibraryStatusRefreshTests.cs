using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Infrastructure.Database;
using GameFocus.Infrastructure.Repositories;
using GameFocus.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameFocus.Tests.Integration;

/// <summary>
/// Reproduces the Play → stale «Pausado» bug. The library receives IUnitOfWork from the root
/// provider (as in App.ConfigureServices), so it keeps one DbContext alive. The session
/// coordinator writes Playing from its own scope. The library must still see that write.
/// </summary>
public class LibraryStatusRefreshTests
{
    private static ServiceProvider BuildRoot(string dbPath)
    {
        var services = new ServiceCollection();
        services.AddDbContext<GameFocusDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IHabitReportRepository, HabitReportRepository>();
        services.AddScoped<ISessionInsightRepository, SessionInsightRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GameStatusWrittenByCoordinatorScope_IsVisibleToLibraryReload()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"gf_status_{Guid.NewGuid():N}.db");
        using var root = BuildRoot(dbPath);

        using (var setup = root.CreateScope())
        {
            var ctx = setup.ServiceProvider.GetRequiredService<GameFocusDbContext>();
            await ctx.Database.EnsureCreatedAsync();
            ctx.Games.Add(new Game { Name = "Silksong", Status = GameStatus.Paused });
            await ctx.SaveChangesAsync();
        }

        // Library: root-resolved UnitOfWork, loaded once.
        var libraryUow = root.GetRequiredService<IUnitOfWork>();
        (await libraryUow.Games.GetAllAsync()).Single().Status.Should().Be(GameStatus.Paused);

        // Coordinator: its own scope marks the game Playing and saves.
        using (var coordinatorScope = root.CreateScope())
        {
            var uow = coordinatorScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var game = (await uow.Games.GetAllAsync()).Single();
            game.Status = GameStatus.Playing;
            uow.Games.Update(game);
            await uow.SaveChangesAsync();
        }

        // Persisted state is correct.
        using (var verify = root.CreateScope())
        {
            var ctx = verify.ServiceProvider.GetRequiredService<GameFocusDbContext>();
            (await ctx.Games.AsNoTracking().SingleAsync()).Status.Should().Be(GameStatus.Playing);
        }

        // The library reload must reflect it too.
        (await libraryUow.Games.GetAllAsync()).Single().Status.Should().Be(GameStatus.Playing);
    }
}

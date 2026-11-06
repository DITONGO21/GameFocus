using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using GameFocus.Application.Interfaces;
using GameFocus.Application.Services;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Infrastructure.Database;
using GameFocus.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameFocus.Tests.Integration;

/// <summary>
/// Reproduces the stale goal progress bug. GoalsViewModel receives IUnitOfWork from the root provider,
/// so it reads goals through one long-lived DbContext. GoalService updates goals from a session scope.
/// The Goals page must show the persisted progress, and reading must not overwrite the related game.
/// </summary>
public class GoalProgressRefreshTests
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
        services.AddTransient<IGoalService, GoalService>();
        return services.BuildServiceProvider();
    }

    private static async Task<ServiceProvider> SeedAsync(string dbPath)
    {
        var root = BuildRoot(dbPath);
        using var scope = root.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<GameFocusDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        var game = new Game { Name = "Silksong", Status = GameStatus.Paused };
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();
        ctx.Goals.Add(new Goal { GameId = game.Id, Title = "30 min", Type = GoalType.PlayMinutes, TargetValue = 60 });
        await ctx.SaveChangesAsync();
        return root;
    }

    [Fact]
    public async Task GoalsPage_ShowsProgressPersistedByGoalService()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"gf_goal_{Guid.NewGuid():N}.db");
        using var root = await SeedAsync(dbPath);

        // Goals page: root-scoped UnitOfWork, loaded before the session ends.
        var goalsUow = root.GetRequiredService<IUnitOfWork>();
        (await goalsUow.Goals.GetActiveGoalsAsync()).Single().CurrentValue.Should().Be(0);

        // Session ends: the game gets 30 minutes and GoalService recalculates progress in its own scope.
        using (var sessionScope = root.CreateScope())
        {
            var uow = sessionScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var game = (await uow.Games.GetAllAsync()).Single();
            game.TotalPlayTime = TimeSpan.FromMinutes(30);
            uow.Games.Update(game);
            await uow.SaveChangesAsync();
            await sessionScope.ServiceProvider.GetRequiredService<IGoalService>()
                .UpdateGoalsAsync(new GameSession { GameId = game.Id });
        }

        // Persisted value is 30 and the Goals page must show the same value.
        using (var verify = root.CreateScope())
        {
            var ctx = verify.ServiceProvider.GetRequiredService<GameFocusDbContext>();
            (await ctx.Goals.AsNoTracking().SingleAsync()).CurrentValue.Should().Be(30);
        }
        (await goalsUow.Goals.GetActiveGoalsAsync()).Single().CurrentValue.Should().Be(30);
    }

    [Fact]
    public async Task CompletingGoalFromStaleReader_DoesNotOverwriteGameRow()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"gf_goal_{Guid.NewGuid():N}.db");
        using var root = await SeedAsync(dbPath);

        // Goals page loads the goal together with its game.
        var goalsUow = root.GetRequiredService<IUnitOfWork>();
        var goal = (await goalsUow.Goals.GetActiveGoalsAsync()).Single();

        // Meanwhile a session updates the game's play time.
        using (var sessionScope = root.CreateScope())
        {
            var uow = sessionScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var game = (await uow.Games.GetAllAsync()).Single();
            game.TotalPlayTime = TimeSpan.FromMinutes(30);
            uow.Games.Update(game);
            await uow.SaveChangesAsync();
        }

        // Completing the goal from the stale page must only write the goal.
        goal.IsCompleted = true;
        goal.CurrentValue = goal.TargetValue;
        goalsUow.Goals.Update(goal);
        await goalsUow.SaveChangesAsync();

        using var verify = root.CreateScope();
        var ctx = verify.ServiceProvider.GetRequiredService<GameFocusDbContext>();
        (await ctx.Games.AsNoTracking().SingleAsync()).TotalPlayTime.Should().Be(TimeSpan.FromMinutes(30));
        (await ctx.Goals.AsNoTracking().SingleAsync()).IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task CreateThenListThenDelete_WorksWithUntrackedReads()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"gf_goal_{Guid.NewGuid():N}.db");
        using var root = await SeedAsync(dbPath);
        var uow = root.GetRequiredService<IUnitOfWork>();

        // Create, as AddGoalAsync does.
        await uow.Goals.AddAsync(new Goal { GameId = 1, Title = "nova", Type = GoalType.PlayMinutes, TargetValue = 10, CreatedAt = DateTime.UtcNow });
        await uow.SaveChangesAsync();

        var listed = (await uow.Goals.GetActiveGoalsAsync()).ToList();
        listed.Should().HaveCount(2);
        listed.Should().OnlyContain(g => g.Game != null && g.Game.Name == "Silksong");

        // Delete, as DeleteGoalAsync does, from the list read above.
        uow.Goals.Delete(listed.Single(g => g.Title == "nova"));
        await uow.SaveChangesAsync();

        using var verify = root.CreateScope();
        var ctx = verify.ServiceProvider.GetRequiredService<GameFocusDbContext>();
        (await ctx.Goals.AsNoTracking().Select(g => g.Title).ToListAsync()).Should().BeEquivalentTo(new[] { "30 min" });
    }
}

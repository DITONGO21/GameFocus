using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using GameFocus.Application.Interfaces;
using GameFocus.Application.Services;
using GameFocus.Application.ViewModels;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Infrastructure.Database;
using GameFocus.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameFocus.Tests.Integration;

/// <summary>
/// A goal created after time was already played must start from the persisted play time,
/// and active goals must be recalculated when the application starts.
/// </summary>
public class GoalCalculationOnCreateAndStartupTests
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

    private static async Task<ServiceProvider> SeedGameWithPlayTimeAsync(string dbPath, TimeSpan playTime)
    {
        var root = BuildRoot(dbPath);
        using var scope = root.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<GameFocusDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        ctx.Games.Add(new Game { Name = "Silksong", Status = GameStatus.Paused, TotalPlayTime = playTime });
        await ctx.SaveChangesAsync();
        return root;
    }

    private static GoalsViewModel CreateGoalsViewModel(ServiceProvider root)
    {
        var uow = root.GetRequiredService<IUnitOfWork>();
        var goalService = root.GetRequiredService<IGoalService>();
        return new GoalsViewModel(uow, goalService);
    }

    [Fact]
    public async Task NewPlayMinutesGoal_StartsFromTimeAlreadyPlayed()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"gf_goalcreate_{Guid.NewGuid():N}.db");
        using var root = await SeedGameWithPlayTimeAsync(dbPath, TimeSpan.FromMinutes(30));
        var vm = CreateGoalsViewModel(root);
        await vm.LoadDataCommand.ExecuteAsync(null);

        vm.SelectedGameForGoal = vm.Games.Single();
        vm.NewGoalTitle = "60 min";
        vm.SelectedGoalTypeIndex = 0; // PlayMinutes
        vm.NewGoalTargetValue = 60;
        await vm.AddGoalCommand.ExecuteAsync(null);

        vm.Goals.Single().CurrentValue.Should().Be(30);
        vm.Goals.Single().IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task NewPlayMinutesGoal_ReachedAlreadyIsCompletedOnCreation()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"gf_goalcreate_{Guid.NewGuid():N}.db");
        using var root = await SeedGameWithPlayTimeAsync(dbPath, TimeSpan.FromMinutes(30));
        var vm = CreateGoalsViewModel(root);
        await vm.LoadDataCommand.ExecuteAsync(null);

        vm.SelectedGameForGoal = vm.Games.Single();
        vm.NewGoalTitle = "20 min";
        vm.SelectedGoalTypeIndex = 0;
        vm.NewGoalTargetValue = 20;
        await vm.AddGoalCommand.ExecuteAsync(null);

        using var verify = root.CreateScope();
        var stored = await verify.ServiceProvider.GetRequiredService<GameFocusDbContext>()
            .Goals.AsNoTracking().SingleAsync();
        // Same calculation as session end: the persisted minutes, not clamped to the target.
        stored.CurrentValue.Should().Be(30);
        stored.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Startup_RecalculatesActiveGoalsFromPersistedPlayTime()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"gf_goalstartup_{Guid.NewGuid():N}.db");
        using (var root = await SeedGameWithPlayTimeAsync(dbPath, TimeSpan.FromMinutes(45)))
        using (var seed = root.CreateScope())
        {
            var ctx = seed.ServiceProvider.GetRequiredService<GameFocusDbContext>();
            ctx.Goals.Add(new Goal { GameId = 1, Title = "60 min", Type = GoalType.PlayMinutes, TargetValue = 60, CurrentValue = 0 });
            await ctx.SaveChangesAsync();
        }

        // Fresh process: the startup routine runs through the coordinator's recalculation entry point.
        using var restarted = BuildRoot(dbPath);
        await restarted.GetRequiredService<IGoalService>().RecalculateGoalsAsync();

        using var verify = restarted.CreateScope();
        var stored = await verify.ServiceProvider.GetRequiredService<GameFocusDbContext>()
            .Goals.AsNoTracking().SingleAsync();
        stored.CurrentValue.Should().Be(45);
        stored.IsCompleted.Should().BeFalse();
    }
}

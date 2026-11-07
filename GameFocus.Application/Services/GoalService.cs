using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GameFocus.Application.Services;

public class GoalService : IGoalService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServiceScopeFactory _scopeFactory;

    public GoalService(IUnitOfWork unitOfWork, IServiceScopeFactory scopeFactory)
    {
        _unitOfWork = unitOfWork;
        _scopeFactory = scopeFactory;
    }

    public Task UpdateGoalsAsync(GameSession session)
    {
        return EvaluateGoalsProgressAsync(_unitOfWork, session.GameId);
    }

    public async Task RecalculateGoalsAsync(int? gameId = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var gameIds = gameId.HasValue
            ? new List<int> { gameId.Value }
            : (await uow.Goals.GetActiveGoalsAsync()).Select(g => g.GameId).Distinct().ToList();

        foreach (var id in gameIds)
        {
            await EvaluateGoalsProgressAsync(uow, id);
        }
    }

    private static async Task EvaluateGoalsProgressAsync(IUnitOfWork uow, int gameId)
    {
        var activeGoals = await uow.Goals.GetByGameIdAsync(gameId);
        var activeUncompletedGoals = activeGoals.Where(g => !g.IsCompleted).ToList();

        if (!activeUncompletedGoals.Any()) return;

        var game = await uow.Games.GetByIdAsync(gameId);
        if (game == null) return;

        foreach (var goal in activeUncompletedGoals)
        {
            bool updated = false;

            switch (goal.Type)
            {
                case GoalType.PlayMinutes:
                    goal.CurrentValue = game.TotalPlayTime.TotalMinutes;
                    if (goal.CurrentValue >= goal.TargetValue)
                    {
                        goal.IsCompleted = true;
                    }
                    updated = true;
                    break;

                case GoalType.PlayConsecutiveDays:
                    // Longest run of consecutive days with at least one session for this game.
                    var sessionsForStreak = await uow.Sessions.GetByGameIdAsync(gameId);
                    goal.CurrentValue = LongestConsecutiveDays(sessionsForStreak.Select(s => s.StartTimeUtc));
                    if (goal.CurrentValue >= goal.TargetValue)
                    {
                        goal.IsCompleted = true;
                    }
                    updated = true;
                    break;

                case GoalType.LoyaltyDays:
                    // Total number of different days on which this game was played.
                    var sessionsForLoyalty = await uow.Sessions.GetByGameIdAsync(gameId);
                    goal.CurrentValue = sessionsForLoyalty.Select(s => s.StartTimeUtc.ToLocalTime().Date).Distinct().Count();
                    if (goal.CurrentValue >= goal.TargetValue)
                    {
                        goal.IsCompleted = true;
                    }
                    updated = true;
                    break;
            }

            if (updated)
            {
                uow.Goals.Update(goal);
            }
        }

        await uow.SaveChangesAsync();
    }

    // Longest streak of consecutive local calendar days that contain at least one of the given session starts.
    public static int LongestConsecutiveDays(IEnumerable<DateTime> sessionStartsUtc)
    {
        var days = sessionStartsUtc
            .Select(d => d.ToLocalTime().Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var best = 0;
        var run = 0;
        DateTime? previous = null;
        foreach (var day in days)
        {
            run = previous.HasValue && (day - previous.Value).TotalDays == 1 ? run + 1 : 1;
            best = Math.Max(best, run);
            previous = day;
        }
        return best;
    }

    // Manual progress for goals that cannot be measured from sessions (campaign, achievements, custom, rank).
    public static void AddManualProgress(Goal goal)
    {
        goal.CurrentValue = Math.Min(goal.CurrentValue + 1, goal.TargetValue);
        if (goal.CurrentValue >= goal.TargetValue)
        {
            goal.IsCompleted = true;
        }
    }
}

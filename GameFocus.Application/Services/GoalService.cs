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
                    // Verificação simplificada de dias consecutivos.
                    // Para uma implementação completa, cruzar com a tabela de sessões.
                    var recentSessions = await uow.Sessions.GetByGameIdAsync(gameId);
                    var daysPlayed = recentSessions.Select(s => s.StartTimeUtc.Date).Distinct().Count();
                    goal.CurrentValue = daysPlayed;
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
}

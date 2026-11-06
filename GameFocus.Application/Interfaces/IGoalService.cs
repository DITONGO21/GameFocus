using System.Threading.Tasks;
using GameFocus.Core.Entities;

namespace GameFocus.Application.Interfaces;

public interface IGoalService
{
    Task UpdateGoalsAsync(GameSession session);

    // Recalculates active goals from persisted play time, in its own scope so no stale entities are read.
    // Pass a game id to recalculate only that game's goals; null recalculates every game with active goals.
    Task RecalculateGoalsAsync(int? gameId = null);
}

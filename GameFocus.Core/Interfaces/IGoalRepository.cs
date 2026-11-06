using System.Collections.Generic;
using System.Threading.Tasks;
using GameFocus.Core.Entities;

namespace GameFocus.Core.Interfaces;

public interface IGoalRepository
{
    Task<Goal?> GetByIdAsync(int id);
    Task<IEnumerable<Goal>> GetByGameIdAsync(int gameId);
    Task<IEnumerable<Goal>> GetActiveGoalsAsync();
    
    Task AddAsync(Goal goal);
    void Update(Goal goal);
    void Delete(Goal goal);
}

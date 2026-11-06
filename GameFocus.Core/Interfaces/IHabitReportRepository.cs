using System.Collections.Generic;
using System.Threading.Tasks;
using GameFocus.Core.Entities;

namespace GameFocus.Core.Interfaces;

public interface IHabitReportRepository
{
    Task<HabitReport?> GetByIdAsync(int id);
    Task<IEnumerable<HabitReport>> GetAllAsync();
    Task<HabitReport?> GetLatestReportAsync();
    
    Task AddAsync(HabitReport report);
}

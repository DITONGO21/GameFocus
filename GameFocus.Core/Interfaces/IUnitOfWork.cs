using System;
using System.Threading.Tasks;

namespace GameFocus.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGameRepository Games { get; }
    IGoalRepository Goals { get; }
    ISessionRepository Sessions { get; }
    IHabitReportRepository HabitReports { get; }
    ISessionInsightRepository SessionInsights { get; }
    
    Task<int> SaveChangesAsync();
}

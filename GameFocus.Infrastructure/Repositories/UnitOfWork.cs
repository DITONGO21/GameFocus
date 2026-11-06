using System.Threading.Tasks;
using GameFocus.Core.Interfaces;
using GameFocus.Infrastructure.Database;

namespace GameFocus.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly GameFocusDbContext _context;
    
    public IGameRepository Games { get; }
    public IGoalRepository Goals { get; }
    public ISessionRepository Sessions { get; }
    public IHabitReportRepository HabitReports { get; }
    public ISessionInsightRepository SessionInsights { get; }

    public UnitOfWork(
        GameFocusDbContext context,
        IGameRepository gameRepository,
        IGoalRepository goalRepository,
        ISessionRepository sessionRepository,
        IHabitReportRepository habitReportRepository,
        ISessionInsightRepository sessionInsightRepository)
    {
        _context = context;
        Games = gameRepository;
        Goals = goalRepository;
        Sessions = sessionRepository;
        HabitReports = habitReportRepository;
        SessionInsights = sessionInsightRepository;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

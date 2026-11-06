using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Infrastructure.Database;

namespace GameFocus.Infrastructure.Repositories;

public class HabitReportRepository : IHabitReportRepository
{
    private readonly GameFocusDbContext _context;

    public HabitReportRepository(GameFocusDbContext context)
    {
        _context = context;
    }

    public async Task<HabitReport?> GetByIdAsync(int id)
    {
        return await _context.HabitReports
            .Include(h => h.MainGame)
            .FirstOrDefaultAsync(h => h.Id == id);
    }

    public async Task<IEnumerable<HabitReport>> GetAllAsync()
    {
        return await _context.HabitReports
            .OrderByDescending(h => h.GeneratedAt)
            .ToListAsync();
    }

    public async Task<HabitReport?> GetLatestReportAsync()
    {
        return await _context.HabitReports
            .OrderByDescending(h => h.GeneratedAt)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(HabitReport report)
    {
        await _context.HabitReports.AddAsync(report);
    }
}

using Microsoft.EntityFrameworkCore;
using GameFocus.Core.Entities;
using System.Reflection;

namespace GameFocus.Infrastructure.Database;

public class GameFocusDbContext : DbContext
{
    public DbSet<Game> Games { get; set; } = null!;
    public DbSet<GameSession> Sessions { get; set; } = null!;
    public DbSet<Goal> Goals { get; set; } = null!;
    public DbSet<HabitReport> HabitReports { get; set; } = null!;
    public DbSet<SessionIntent> SessionIntents { get; set; } = null!;
    public DbSet<SessionFeedback> SessionFeedbacks { get; set; } = null!;

    public GameFocusDbContext(DbContextOptions<GameFocusDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

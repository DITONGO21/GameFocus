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
using Moq;
using Xunit;

namespace GameFocus.Tests.Integration;

// Removing a game from the library, using the same long-lived context the library page uses.
public class DeleteGameTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"gf_delete_{Guid.NewGuid():N}");
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"gf_delete_{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _root;

    public DeleteGameTests()
    {
        Directory.CreateDirectory(_folder);
        var services = new ServiceCollection();
        services.AddDbContext<GameFocusDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IHabitReportRepository, HabitReportRepository>();
        services.AddScoped<ISessionInsightRepository, SessionInsightRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddTransient<GameDiscoveryService>(sp =>
            new GameDiscoveryService(sp.GetRequiredService<IUnitOfWork>(), Array.Empty<IGameSource>()));
        _root = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _root.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task RemoveGame_AddedAndListedInSameContext_DoesNotThrow()
    {
        using (var init = _root.CreateScope())
        {
            await init.ServiceProvider.GetRequiredService<GameFocusDbContext>().Database.EnsureCreatedAsync();
        }
        var exe = Path.Combine(_folder, "Remove.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A });
        var uow = _root.GetRequiredService<IUnitOfWork>();
        var vm = new LibraryViewModel(uow, _root.GetRequiredService<GameDiscoveryService>(), Mock.Of<IDashboardPublisher>(),
            Mock.Of<IUIDispatcher>(), Mock.Of<ISettingsService>(), NullLogger<LibraryViewModel>.Instance);

        await vm.AddExecutableGameAsync(exe, "Remove me", null);
        var listed = vm.Games.Single();

        var act = () => vm.DeleteGameCommand.ExecuteAsync(listed);

        await act.Should().NotThrowAsync();
        vm.Games.Should().BeEmpty();
        using var verify = _root.CreateScope();
        (await verify.ServiceProvider.GetRequiredService<GameFocusDbContext>().Games.CountAsync()).Should().Be(0);
    }
}

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

// Manual games: add from an executable, edit name and path, duplicate and invalid input rules.
public class ManualGameManagementTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"gf_manual_{Guid.NewGuid():N}");
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"gf_manual_{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _root;

    public ManualGameManagementTests()
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

    private string Exe(string name)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, new byte[] { 0x4D, 0x5A });
        return path;
    }

    private async Task EnsureSchemaAsync()
    {
        using var scope = _root.CreateScope();
        await scope.ServiceProvider.GetRequiredService<GameFocusDbContext>().Database.EnsureCreatedAsync();
    }

    private GameDiscoveryService Discovery() => _root.GetRequiredService<GameDiscoveryService>();

    private LibraryViewModel LibraryVm()
    {
        var uow = _root.GetRequiredService<IUnitOfWork>();
        var discovery = Discovery();
        return new LibraryViewModel(uow, discovery, Mock.Of<IDashboardPublisher>(),
            Mock.Of<IUIDispatcher>(), Mock.Of<ISettingsService>(), NullLogger<LibraryViewModel>.Instance);
    }

    [Fact]
    public async Task Add_StoresManualGameWithFullExecutablePath()
    {
        await EnsureSchemaAsync();
        var exe = Exe("Hytale.exe");

        var game = await Discovery().AddExecutableGameAsync(exe, "Hytale", "Hypixel");

        game.Source.Should().Be(GameSourceType.Manual);
        game.ExternalId.Should().BeNull();
        game.ExecutablePath.Should().Be(Path.GetFullPath(exe));
        game.Developer.Should().Be("Hypixel");
    }

    [Fact]
    public async Task Add_RejectsExecutableAlreadyStored_IgnoringCase()
    {
        await EnsureSchemaAsync();
        var exe = Exe("Star.exe");
        await Discovery().AddExecutableGameAsync(exe, "Star Citizen");

        var act = () => Discovery().AddExecutableGameAsync(exe.ToLowerInvariant(), "Star Citizen again");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Star Citizen*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Add_RejectsEmptyName(string name)
    {
        await EnsureSchemaAsync();
        var exe = Exe("Empty.exe");

        var act = () => Discovery().AddExecutableGameAsync(exe, name);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Add_RejectsNonExecutableFile()
    {
        await EnsureSchemaAsync();
        var txt = Path.Combine(_folder, "readme.txt");
        File.WriteAllText(txt, "not a game");

        var act = () => Discovery().AddExecutableGameAsync(txt, "Readme");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*.exe*");
    }

    [Fact]
    public async Task Update_ChangesNameAndPathOfManualGame()
    {
        await EnsureSchemaAsync();
        var original = Exe("Old.exe");
        var game = await Discovery().AddExecutableGameAsync(original, "Old name");
        var moved = Exe("New.exe");

        await Discovery().UpdateManualGameAsync(game.Id, "New name", moved);

        using var verify = _root.CreateScope();
        var stored = await verify.ServiceProvider.GetRequiredService<GameFocusDbContext>()
            .Games.AsNoTracking().SingleAsync(g => g.Id == game.Id);
        stored.Name.Should().Be("New name");
        stored.ExecutablePath.Should().Be(Path.GetFullPath(moved));
        stored.ExecutableName.Should().Be("New.exe");
        stored.Source.Should().Be(GameSourceType.Manual);
    }

    [Fact]
    public async Task Update_KeepingSamePathIsAllowed()
    {
        await EnsureSchemaAsync();
        var exe = Exe("Same.exe");
        var game = await Discovery().AddExecutableGameAsync(exe, "Same");

        var act = () => Discovery().UpdateManualGameAsync(game.Id, "Renamed", exe);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Update_RejectsPathUsedByAnotherGame()
    {
        await EnsureSchemaAsync();
        var first = Exe("First.exe");
        var second = Exe("Second.exe");
        await Discovery().AddExecutableGameAsync(first, "First");
        var other = await Discovery().AddExecutableGameAsync(second, "Second");

        var act = () => Discovery().UpdateManualGameAsync(other.Id, "Second", first);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*First*");
    }

    [Fact]
    public async Task Update_RejectsNonManualGame()
    {
        await EnsureSchemaAsync();
        using (var scope = _root.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<GameFocusDbContext>();
            ctx.Games.Add(new Game { Name = "ULTRAKILL", Source = GameSourceType.Steam, ExternalId = "1229490", SteamAppId = 1229490 });
            await ctx.SaveChangesAsync();
        }
        var steamId = await _root.CreateScope().ServiceProvider.GetRequiredService<GameFocusDbContext>()
            .Games.AsNoTracking().Select(g => g.Id).SingleAsync();

        var act = () => Discovery().UpdateManualGameAsync(steamId, "Renamed", Exe("Any.exe"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*manually*");
    }

    [Fact]
    public async Task ViewModel_ReturnsValidationMessage_AndSucceedsWithValidInput()
    {
        await EnsureSchemaAsync();
        var vm = LibraryVm();
        var exe = Exe("Vm.exe");

        (await vm.AddExecutableGameAsync(Path.Combine(_folder, "missing.exe"), "Missing", null))
            .Should().NotBeNull();
        (await vm.AddExecutableGameAsync(exe, "Vm game", null)).Should().BeNull();

        vm.Games.Should().ContainSingle(g => g.Name == "Vm game" && g.Source == GameSourceType.Manual);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using GameFocus.Application.Interfaces;
using GameFocus.Application.Services;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Infrastructure.Database;
using GameFocus.Infrastructure.GameSources;
using GameFocus.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GameFocus.Tests.Integration;

public class GameDiscoveryTests
{
    private static ServiceProvider BuildRoot(string dbPath, params IGameSource[] sources)
    {
        var services = new ServiceCollection();
        services.AddDbContext<GameFocusDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IHabitReportRepository, HabitReportRepository>();
        services.AddScoped<ISessionInsightRepository, SessionInsightRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddTransient<GameDiscoveryService>(sp =>
            new GameDiscoveryService(sp.GetRequiredService<IUnitOfWork>(), sources));
        return services.BuildServiceProvider();
    }

    private static string NewDbPath() => Path.Combine(Path.GetTempPath(), $"gf_discovery_{Guid.NewGuid():N}.db");

    private static async Task EnsureSchemaAsync(ServiceProvider root)
    {
        using var scope = root.CreateScope();
        await scope.ServiceProvider.GetRequiredService<GameFocusDbContext>().Database.EnsureCreatedAsync();
    }

    private sealed class FakeSource : IGameSource
    {
        private readonly IReadOnlyList<Game> _games;

        public FakeSource(GameSourceType source, params Game[] games)
        {
            Source = source;
            _games = games;
        }

        public GameSourceType Source { get; }

        public Task<IReadOnlyList<Game>> DiscoverAsync() => Task.FromResult(_games);
    }

    private static Game Discovered(GameSourceType source, string externalId, string name) =>
        new() { Source = source, ExternalId = externalId, Name = name, Status = GameStatus.Paused };

    [Fact]
    public async Task Import_StoresEachSourceGame_AndIsIdempotent()
    {
        var source = new FakeSource(GameSourceType.Epic,
            Discovered(GameSourceType.Epic, "fortnite", "Fortnite"),
            Discovered(GameSourceType.Epic, "rocket", "Rocket League"));
        using var root = BuildRoot(NewDbPath(), source);
        await EnsureSchemaAsync(root);

        var discovery = root.GetRequiredService<GameDiscoveryService>();
        (await discovery.ImportFromSourcesAsync()).Should().Be(2);
        (await discovery.ImportFromSourcesAsync()).Should().Be(0);

        using var verify = root.CreateScope();
        var stored = await verify.ServiceProvider.GetRequiredService<GameFocusDbContext>()
            .Games.AsNoTracking().ToListAsync();
        stored.Should().HaveCount(2);
        stored.Should().OnlyContain(g => g.Source == GameSourceType.Epic && g.ExternalId != null);
    }

    [Fact]
    public async Task Import_DoesNotDuplicateGameAlreadyStoredFromSameSource()
    {
        var dbPath = NewDbPath();
        using (var seed = BuildRoot(dbPath))
        {
            await EnsureSchemaAsync(seed);
            using var scope = seed.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<GameFocusDbContext>();
            ctx.Games.Add(Discovered(GameSourceType.Steam, "1229490", "ULTRAKILL"));
            await ctx.SaveChangesAsync();
        }

        var source = new FakeSource(GameSourceType.Steam, Discovered(GameSourceType.Steam, "1229490", "ULTRAKILL"));
        using var root = BuildRoot(dbPath, source);
        (await root.GetRequiredService<GameDiscoveryService>().ImportFromSourcesAsync()).Should().Be(0);

        using var verify = root.CreateScope();
        (await verify.ServiceProvider.GetRequiredService<GameFocusDbContext>().Games.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Import_SkipsGamesWithoutExternalId()
    {
        var source = new FakeSource(GameSourceType.Epic, new Game { Name = "No id", Source = GameSourceType.Epic });
        using var root = BuildRoot(NewDbPath(), source);
        await EnsureSchemaAsync(root);

        (await root.GetRequiredService<GameDiscoveryService>().ImportFromSourcesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddExecutable_StoresManualGameOnce()
    {
        var exe = Path.Combine(Path.GetTempPath(), $"GfFakeGame_{Guid.NewGuid():N}.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A });
        try
        {
            using var root = BuildRoot(NewDbPath());
            await EnsureSchemaAsync(root);
            var discovery = root.GetRequiredService<GameDiscoveryService>();

            var first = await discovery.AddExecutableGameAsync(exe, "Fake Game");

            first.Source.Should().Be(GameSourceType.Manual);
            first.ExecutablePath.Should().Be(Path.GetFullPath(exe));

            var duplicate = () => discovery.AddExecutableGameAsync(exe.ToUpperInvariant(), "Again");
            await duplicate.Should().ThrowAsync<InvalidOperationException>();

            using var verify = root.CreateScope();
            (await verify.ServiceProvider.GetRequiredService<GameFocusDbContext>().Games.CountAsync()).Should().Be(1);
        }
        finally
        {
            File.Delete(exe);
        }
    }

    [Fact]
    public async Task AddExecutable_RejectsMissingFile()
    {
        using var root = BuildRoot(NewDbPath());
        await EnsureSchemaAsync(root);

        var act = () => root.GetRequiredService<GameDiscoveryService>()
            .AddExecutableGameAsync(@"C:\does\not\exist.exe", "Missing");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Migrations_ApplyOnFreshDatabase_AndCreateUniqueSourceIndex()
    {
        using var root = BuildRoot(NewDbPath());
        using (var scope = root.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<GameFocusDbContext>().Database.MigrateAsync();
        }

        using var verify = root.CreateScope();
        var ctx = verify.ServiceProvider.GetRequiredService<GameFocusDbContext>();
        var indexes = await ctx.Database.SqlQueryRaw<string>(
                "SELECT name AS Value FROM sqlite_master WHERE type = 'index' AND name = 'IX_Games_Source_ExternalId'")
            .ToListAsync();
        indexes.Should().ContainSingle();
    }

    [Fact]
    public async Task SteamSource_MapsSteamGamesToSourceAndExternalId()
    {
        var reader = new Mock<ISteamLibraryReader>();
        reader.Setup(r => r.IsSteamInstalledAsync()).ReturnsAsync(true);
        reader.Setup(r => r.GetInstalledGamesAsync()).ReturnsAsync(new List<Game>
        {
            new() { SteamAppId = 1229490, Name = "ULTRAKILL" },
            new() { Name = "No app id" }
        });

        var games = await new SteamGameSource(reader.Object).DiscoverAsync();

        games.Should().ContainSingle();
        games[0].Source.Should().Be(GameSourceType.Steam);
        games[0].ExternalId.Should().Be("1229490");
    }

    [Fact]
    public async Task SteamSource_ReturnsNothingWhenSteamIsNotInstalled()
    {
        var reader = new Mock<ISteamLibraryReader>();
        reader.Setup(r => r.IsSteamInstalledAsync()).ReturnsAsync(false);

        var games = await new SteamGameSource(reader.Object).DiscoverAsync();

        games.Should().BeEmpty();
        reader.Verify(r => r.GetInstalledGamesAsync(), Times.Never);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using GameFocus.Application.Interfaces;
using GameFocus.Application.Services;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameFocus.Tests.Integration;

/// <summary>
/// End-to-end integration tests for SessionCoordinator.
/// Uses a fake IGameSessionService that we control (raising events manually)
/// combined with a real DI container and mock repositories.
/// Tests the full pipeline: event → coordinator → DB calls → dashboard publish.
/// </summary>
public class SessionCoordinatorE2ETests
{
    private Mock<IGameSessionService> _detectorMock = null!;
    private Mock<IUnitOfWork> _uowMock = null!;
    private Mock<IGameRepository> _gameRepo = null!;
    private Mock<ISessionRepository> _sessionRepo = null!;
    private Mock<IDashboardPublisher> _publisherMock = null!;
    private SessionCoordinator _coordinator = null!;

    private void Setup(List<Game> games, List<GameSession> sessions)
    {
        _detectorMock = new Mock<IGameSessionService>();
        _uowMock = new Mock<IUnitOfWork>();
        _gameRepo = new Mock<IGameRepository>();
        _sessionRepo = new Mock<ISessionRepository>();
        _publisherMock = new Mock<IDashboardPublisher>();

        _uowMock.Setup(u => u.Games).Returns(_gameRepo.Object);
        _uowMock.Setup(u => u.Sessions).Returns(_sessionRepo.Object);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        _gameRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(games);
        _sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(sessions);

        foreach (var g in games)
            _gameRepo.Setup(r => r.GetByIdAsync(g.Id)).ReturnsAsync(g);

        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => _uowMock.Object);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        _coordinator = new SessionCoordinator(
            _detectorMock.Object,
            scopeFactory,
            _publisherMock.Object,
            NullLogger<SessionCoordinator>.Instance);
    }

    [Fact]
    public async Task E2E_FullSessionLifecycle_StartPlayStop()
    {
        // Arrange
        var game = new Game { Id = 1, Name = "testgame", Status = GameStatus.Playing, TotalPlayTime = TimeSpan.Zero };
        var session = new GameSession { Id = 50, GameId = 1, StartTimeUtc = DateTime.UtcNow.AddMinutes(-2) };

        Setup(new List<Game> { game }, new List<GameSession>());

        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<GameSession>()))
            .Callback<GameSession>(s => { s.Id = 50; })
            .Returns(Task.CompletedTask);
        _sessionRepo.Setup(r => r.GetByIdAsync(50)).ReturnsAsync(session);

        await _coordinator.InitializeAsync();

        // Act: start game
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "testgame");
        await Task.Delay(300);

        // Assert: session created, dashboard notified
        _sessionRepo.Verify(r => r.AddAsync(It.IsAny<GameSession>()), Times.Once);
        _publisherMock.Verify(p => p.PublishSessionUpdated(It.IsAny<object>()), Times.Once);

        // Act: stop game
        _detectorMock.Raise(d => d.OnGameClosed += null, _detectorMock.Object, "testgame");
        await Task.Delay(300);

        // Assert: session ended, game stats updated, dashboard notified again
        session.EndTimeUtc.Should().NotBeNull();
        session.DurationSeconds.Should().BeGreaterOrEqualTo(0);
        game.LastPlayed.Should().NotBeNull();
        game.TotalPlayTime.Should().BeGreaterThan(TimeSpan.Zero);
        _publisherMock.Verify(p => p.PublishSessionUpdated(It.IsAny<object>()), Times.Exactly(2));
    }

    [Fact]
    public async Task E2E_TwoGamesInSequence()
    {
        // Arrange
        var game1 = new Game { Id = 1, Name = "game1", Status = GameStatus.Playing, TotalPlayTime = TimeSpan.Zero };
        var game2 = new Game { Id = 2, Name = "game2", Status = GameStatus.Playing, TotalPlayTime = TimeSpan.Zero };
        var session1 = new GameSession { Id = 50, GameId = 1, StartTimeUtc = DateTime.UtcNow.AddMinutes(-5) };
        var session2 = new GameSession { Id = 51, GameId = 2, StartTimeUtc = DateTime.UtcNow.AddMinutes(-1) };

        Setup(new List<Game> { game1, game2 }, new List<GameSession>());

        int nextId = 50;
        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<GameSession>()))
            .Callback<GameSession>(s => { s.Id = nextId++; })
            .Returns(Task.CompletedTask);
        _sessionRepo.Setup(r => r.GetByIdAsync(50)).ReturnsAsync(session1);
        _sessionRepo.Setup(r => r.GetByIdAsync(51)).ReturnsAsync(session2);

        await _coordinator.InitializeAsync();

        // Act: play game1, close it, play game2, close it
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "game1");
        await Task.Delay(300);
        _detectorMock.Raise(d => d.OnGameClosed += null, _detectorMock.Object, "game1");
        await Task.Delay(300);

        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "game2");
        await Task.Delay(300);
        _detectorMock.Raise(d => d.OnGameClosed += null, _detectorMock.Object, "game2");
        await Task.Delay(300);

        // Assert
        session1.EndTimeUtc.Should().NotBeNull();
        session2.EndTimeUtc.Should().NotBeNull();
        game1.LastPlayed.Should().NotBeNull();
        game2.LastPlayed.Should().NotBeNull();
        _sessionRepo.Verify(r => r.AddAsync(It.IsAny<GameSession>()), Times.Exactly(2));
    }

    [Fact(Skip = "Fast Switch removido. O sistema suporta múltiplas sessões.")]
    public async Task E2E_FastSwitchBetweenGames_ClosesFirst()
    {
        // Arrange
        var game1 = new Game { Id = 1, Name = "game1", Status = GameStatus.Playing, TotalPlayTime = TimeSpan.Zero };
        var game2 = new Game { Id = 2, Name = "game2", Status = GameStatus.Playing, TotalPlayTime = TimeSpan.Zero };
        var session1 = new GameSession { Id = 50, GameId = 1, StartTimeUtc = DateTime.UtcNow.AddMinutes(-3) };

        Setup(new List<Game> { game1, game2 }, new List<GameSession>());

        int nextId = 50;
        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<GameSession>()))
            .Callback<GameSession>(s => { s.Id = nextId++; })
            .Returns(Task.CompletedTask);
        _sessionRepo.Setup(r => r.GetByIdAsync(50)).ReturnsAsync(session1);
        _gameRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(game1);

        await _coordinator.InitializeAsync();

        // Act: start game1, then immediately start game2 (fast switch)
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "game1");
        await Task.Delay(300);
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "game2");
        await Task.Delay(500);

        // Assert: game1's session should have been terminated
        session1.EndTimeUtc.Should().NotBeNull("fast switch should close the previous game's session");
        game1.TotalPlayTime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task E2E_DanglingSessionRecovery_OnRestart()
    {
        // Arrange: simulate dangling session from previous crash
        var game = new Game { Id = 1, Name = "testgame", Status = GameStatus.Playing };
        var danglingSession = new GameSession
        {
            Id = 99,
            GameId = 1,
            StartTimeUtc = DateTime.UtcNow.AddHours(-1),
            EndTimeUtc = null  // crashed while game was running
        };

        Setup(new List<Game> { game }, new List<GameSession> { danglingSession });

        // Act: initialize (simulates app restart)
        await _coordinator.InitializeAsync();

        // Assert: dangling session should be fixed
        danglingSession.EndTimeUtc.Should().NotBeNull("dangling sessions should be closed on restart");
        danglingSession.DurationSeconds.Should().Be(0, "recovered sessions get 0 duration");
        _sessionRepo.Verify(r => r.Update(danglingSession), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task E2E_UnexpectedClose_NoMultipleActiveSessions()
    {
        // Arrange
        var game = new Game { Id = 1, Name = "testgame", Status = GameStatus.Playing, TotalPlayTime = TimeSpan.Zero };

        Setup(new List<Game> { game }, new List<GameSession>());

        int nextId = 50;
        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<GameSession>()))
            .Callback<GameSession>(s => { s.Id = nextId++; })
            .Returns(Task.CompletedTask);

        await _coordinator.InitializeAsync();

        // Act: start game
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "testgame");
        await Task.Delay(300);

        // Simulate unexpected close by NOT raising OnGameClosed
        // Instead, dispose and reinitialize with the dangling session
        _coordinator.Dispose();

        // Now create a new coordinator with the dangling session
        var danglingSession = new GameSession
        {
            Id = 50,
            GameId = 1,
            StartTimeUtc = DateTime.UtcNow.AddMinutes(-2),
            EndTimeUtc = null
        };

        Setup(new List<Game> { game }, new List<GameSession> { danglingSession });
        await _coordinator.InitializeAsync();

        // Assert
        danglingSession.EndTimeUtc.Should().NotBeNull("restarting should close dangling sessions");
    }
}

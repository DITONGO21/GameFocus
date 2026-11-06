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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameFocus.Tests.Unit;

public class SessionCoordinatorTests
{
    private readonly Mock<IGameSessionService> _detectorMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGameRepository> _gameRepoMock;
    private readonly Mock<ISessionRepository> _sessionRepoMock;
    private readonly Mock<IDashboardPublisher> _publisherMock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SessionCoordinator _coordinator;

    public SessionCoordinatorTests()
    {
        _detectorMock = new Mock<IGameSessionService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _gameRepoMock = new Mock<IGameRepository>();
        _sessionRepoMock = new Mock<ISessionRepository>();
        _publisherMock = new Mock<IDashboardPublisher>();

        _unitOfWorkMock.Setup(u => u.Games).Returns(_gameRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Sessions).Returns(_sessionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Build a real ServiceProvider with a mock IUnitOfWork
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => _unitOfWorkMock.Object);
        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        _coordinator = new SessionCoordinator(
            _detectorMock.Object,
            _scopeFactory,
            _publisherMock.Object,
            NullLogger<SessionCoordinator>.Instance);
    }

    [Fact]
    public async Task InitializeAsync_LoadsGamesAndStartsMonitoring()
    {
        // Arrange
        var games = new List<Game>
        {
            new Game { Id = 1, Name = "notepad", Status = GameStatus.Playing },
            new Game { Id = 2, Name = "calc", Status = GameStatus.Playing }
        };
        _gameRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(games);
        _sessionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<GameSession>());

        // Act
        await _coordinator.InitializeAsync();

        // Assert
        _detectorMock.Verify(d => d.UpdateWatchList(It.Is<IEnumerable<string>>(
            list => list.Contains("notepad") && list.Contains("calc"))), Times.Once);
        _detectorMock.Verify(d => d.StartMonitoring(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_ClosesDanglingSessions()
    {
        // Arrange
        var games = new List<Game> { new Game { Id = 1, Name = "notepad" } };
        var danglingSession = new GameSession
        {
            Id = 10,
            GameId = 1,
            StartTimeUtc = DateTime.UtcNow.AddHours(-2),
            EndTimeUtc = null // dangling!
        };
        _gameRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(games);
        _sessionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<GameSession> { danglingSession });

        // Act
        await _coordinator.InitializeAsync();

        // Assert
        danglingSession.EndTimeUtc.Should().NotBeNull();
        danglingSession.DurationSeconds.Should().Be(0);
        _sessionRepoMock.Verify(r => r.Update(danglingSession), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task OnGameStarted_CreatesSessionInDatabase()
    {
        // Arrange: setup DB with a game
        var game = new Game { Id = 1, Name = "notepad", Status = GameStatus.Playing };
        _gameRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game> { game });
        _sessionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<GameSession>());
        _sessionRepoMock.Setup(r => r.AddAsync(It.IsAny<GameSession>()))
            .Callback<GameSession>(s => s.Id = 100)
            .Returns(Task.CompletedTask);

        await _coordinator.InitializeAsync();

        // Act: raise the OnGameStarted event
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "notepad");
        await Task.Delay(200); // allow async handler to run

        // Assert
        _sessionRepoMock.Verify(r => r.AddAsync(It.Is<GameSession>(s =>
            s.GameId == 1 && s.EndTimeUtc == null)), Times.Once);
        // SaveChangesAsync: once during init (no dangling sessions), once during start (session add)
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeast(1));
    }

    [Fact]
    public async Task OnGameClosed_UpdatesSessionWithEndTimeAndDuration()
    {
        // Arrange
        var game = new Game { Id = 1, Name = "notepad", Status = GameStatus.Playing, TotalPlayTime = TimeSpan.Zero };
        var session = new GameSession { Id = 100, GameId = 1, StartTimeUtc = DateTime.UtcNow.AddMinutes(-5) };

        _gameRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game> { game });
        _sessionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<GameSession>());
        _sessionRepoMock.Setup(r => r.AddAsync(It.IsAny<GameSession>()))
            .Callback<GameSession>(s => { s.Id = 100; })
            .Returns(Task.CompletedTask);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(session);
        _gameRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(game);

        await _coordinator.InitializeAsync();

        // Start a session first
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "notepad");
        await Task.Delay(200);

        // Act: close the game
        _detectorMock.Raise(d => d.OnGameClosed += null, _detectorMock.Object, "notepad");
        await Task.Delay(200);

        // Assert
        session.EndTimeUtc.Should().NotBeNull();
        session.DurationSeconds.Should().BeGreaterThan(0);
        _sessionRepoMock.Verify(r => r.Update(session), Times.Once);
        game.LastPlayed.Should().NotBeNull();
        game.TotalPlayTime.TotalMinutes.Should().BeGreaterThan(0);
        game.Status.Should().Be(GameStatus.Paused);
    }

    [Fact]
    public async Task InitializeAsync_ClearsStalePlayingStatus_WhenDanglingSessionExists()
    {
        // Arrange
        var game = new Game { Id = 1, Name = "silksong", Status = GameStatus.Playing, TotalPlayTime = TimeSpan.Zero };
        var danglingSession = new GameSession
        {
            Id = 99,
            GameId = 1,
            Game = game,
            StartTimeUtc = DateTime.UtcNow.AddHours(-2),
            EndTimeUtc = null,
            Status = SessionStatus.Active
        };

        _gameRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game> { game });
        _sessionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<GameSession> { danglingSession });

        // Act
        await _coordinator.InitializeAsync();

        // Assert
        danglingSession.EndTimeUtc.Should().NotBeNull();
        danglingSession.Status.Should().Be(SessionStatus.Interrupted);
        game.Status.Should().Be(GameStatus.Paused);
        _sessionRepoMock.Verify(r => r.Update(danglingSession), Times.Once);
        _gameRepoMock.Verify(r => r.Update(game), Times.AtLeastOnce);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeast(1));
    }

    [Fact]
    public async Task DuplicateStartEvents_DoNotCreateMultipleSessions()
    {
        // Arrange
        var game = new Game { Id = 1, Name = "notepad", Status = GameStatus.Playing };
        _gameRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game> { game });
        _sessionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<GameSession>());
        _sessionRepoMock.Setup(r => r.AddAsync(It.IsAny<GameSession>()))
            .Callback<GameSession>(s => s.Id = 100)
            .Returns(Task.CompletedTask);

        await _coordinator.InitializeAsync();

        // Act: fire start twice
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "notepad");
        await Task.Delay(200);
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "notepad");
        await Task.Delay(200);

        // Assert: only one session created
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<GameSession>()), Times.Once);
    }

    [Fact(Skip = "Fast Switch removido. Múltiplas sessões permitidas.")]
    public async Task FastSwitch_ClosesFirstGame_WhenSecondStarts()
    {
        // Arrange
        var game1 = new Game { Id = 1, Name = "notepad", Status = GameStatus.Playing, TotalPlayTime = TimeSpan.Zero };
        var game2 = new Game { Id = 2, Name = "calc", Status = GameStatus.Playing, TotalPlayTime = TimeSpan.Zero };
        var session1 = new GameSession { Id = 100, GameId = 1, StartTimeUtc = DateTime.UtcNow.AddMinutes(-3) };

        _gameRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game> { game1, game2 });
        _sessionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<GameSession>());

        int nextSessionId = 100;
        _sessionRepoMock.Setup(r => r.AddAsync(It.IsAny<GameSession>()))
            .Callback<GameSession>(s => s.Id = nextSessionId++)
            .Returns(Task.CompletedTask);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(session1);
        _gameRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(game1);
        _gameRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(game2);

        await _coordinator.InitializeAsync();

        // Act: start game1, then immediately start game2 (fast switch)
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "notepad");
        await Task.Delay(300);
        _detectorMock.Raise(d => d.OnGameStarted += null, _detectorMock.Object, "calc");
        await Task.Delay(500);

        // Assert: session1 should have been closed (EndTime set)
        session1.EndTimeUtc.Should().NotBeNull("the first game session should be ended when a new game starts");
        _sessionRepoMock.Verify(r => r.Update(session1), Times.Once);
    }

    [Fact]
    public async Task StopEvent_ForUnknownProcess_IsIgnored()
    {
        // Arrange
        _gameRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game>());
        _sessionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<GameSession>());

        await _coordinator.InitializeAsync();

        // Act: close event for unknown process
        _detectorMock.Raise(d => d.OnGameClosed += null, _detectorMock.Object, "unknown_game");
        await Task.Delay(200);

        // Assert: no DB calls
        _sessionRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }
}

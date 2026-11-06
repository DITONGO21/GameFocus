using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using GameFocus.Application.Interfaces;
using GameFocus.Application.ViewModels;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameFocus.Tests.Unit;

public class DashboardFocusGameTests
{
    private static DashboardViewModel CreateViewModel(List<Game> games, int? focusGameId)
    {
        var gameRepo = new Mock<IGameRepository>();
        gameRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(games);

        var sessionRepo = new Mock<ISessionRepository>();
        sessionRepo.Setup(r => r.GetSessionsInDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<GameSession>());

        var goalRepo = new Mock<IGoalRepository>();
        goalRepo.Setup(r => r.GetActiveGoalsAsync()).ReturnsAsync(new List<Goal>());

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Games).Returns(gameRepo.Object);
        unitOfWork.Setup(u => u.Sessions).Returns(sessionRepo.Object);
        unitOfWork.Setup(u => u.Goals).Returns(goalRepo.Object);

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetFocusGameId()).Returns(focusGameId);

        return new DashboardViewModel(
            unitOfWork.Object,
            Mock.Of<IGameSessionService>(),
            Mock.Of<IDatabaseInitializer>(),
            Mock.Of<IDashboardPublisher>(),
            Mock.Of<IUIDispatcher>(),
            null!,
            settings.Object,
            NullLogger<DashboardViewModel>.Instance);
    }

    [Fact]
    public async Task FocusGameChosenInLibrary_IsShownOnDashboard()
    {
        var games = new List<Game>
        {
            new() { Id = 1, Name = "Playing now", Status = GameStatus.Playing },
            new() { Id = 2, Name = "Chosen focus", Status = GameStatus.Paused }
        };
        var viewModel = CreateViewModel(games, focusGameId: 2);

        await viewModel.LoadDataCommand.ExecuteAsync(null);

        viewModel.MainGameName.Should().Be("Chosen focus");
    }

    [Fact]
    public async Task NoFocusChosen_FallsBackToGameBeingPlayed()
    {
        var games = new List<Game>
        {
            new() { Id = 1, Name = "Idle", Status = GameStatus.Paused },
            new() { Id = 2, Name = "Playing now", Status = GameStatus.Playing }
        };
        var viewModel = CreateViewModel(games, focusGameId: null);

        await viewModel.LoadDataCommand.ExecuteAsync(null);

        viewModel.MainGameName.Should().Be("Playing now");
    }

    [Fact]
    public async Task FocusGameNotBeingPlayed_ShowsPausedStatus()
    {
        var games = new List<Game>
        {
            new() { Id = 2, Name = "Chosen focus", Status = GameStatus.Paused }
        };
        var viewModel = CreateViewModel(games, focusGameId: 2);

        await viewModel.LoadDataCommand.ExecuteAsync(null);

        viewModel.MainGameStatus.Should().Be("Paused");
    }

    [Fact]
    public async Task FocusGameBeingPlayed_ShowsPlayingStatus()
    {
        var games = new List<Game>
        {
            new() { Id = 2, Name = "Chosen focus", Status = GameStatus.Playing }
        };
        var viewModel = CreateViewModel(games, focusGameId: 2);

        await viewModel.LoadDataCommand.ExecuteAsync(null);

        viewModel.MainGameStatus.Should().Be("Playing now");
    }
}

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

public class LibraryViewModelTests
{
    [Fact]
    public async Task PublisherSessionEnded_ReloadsLibraryAndClearsPlayingStatus()
    {
        var game = new Game { Id = 1, Name = "Silksong", Status = GameStatus.Playing };
        var pausedGame = new Game { Id = 1, Name = "Silksong", Status = GameStatus.Paused };

        var gameRepo = new Mock<IGameRepository>();
        gameRepo.SetupSequence(r => r.GetAllAsync())
            .ReturnsAsync(new List<Game> { game })
            .ReturnsAsync(new List<Game> { pausedGame });

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Games).Returns(gameRepo.Object);

        var publisher = new Mock<IDashboardPublisher>();
        var dispatcher = new Mock<IUIDispatcher>();
        dispatcher.Setup(d => d.Enqueue(It.IsAny<System.Action>()))
            .Callback<System.Action>(a => a());

        var viewModel = new LibraryViewModel(
            unitOfWork.Object,
            new GameFocus.Application.Services.GameDiscoveryService(Mock.Of<IUnitOfWork>(), System.Array.Empty<IGameSource>()),
            publisher.Object,
            dispatcher.Object,
            Mock.Of<ISettingsService>(),
            NullLogger<LibraryViewModel>.Instance);

        await viewModel.LoadGamesAsync();
        viewModel.Games.Should().ContainSingle(g => g.Status == GameStatus.Playing);

        publisher.Raise(p => p.OnSessionUpdated += null, publisher.Object, new { Event = "SessionEnded", Game = game.Name });

        viewModel.Games.Should().ContainSingle(g => g.Status == GameStatus.Paused);
    }

    [Fact]
    public async Task SetAsFocus_PersistsChosenGameAndKeepsRuntimeStatus()
    {
        var game = new Game { Id = 7, Name = "Hades II", Status = GameStatus.Paused };

        var gameRepo = new Mock<IGameRepository>();
        gameRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game> { game });
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Games).Returns(gameRepo.Object);

        var settings = new Mock<ISettingsService>();
        var viewModel = new LibraryViewModel(
            unitOfWork.Object,
            new GameFocus.Application.Services.GameDiscoveryService(Mock.Of<IUnitOfWork>(), System.Array.Empty<IGameSource>()),
            Mock.Of<IDashboardPublisher>(),
            Mock.Of<IUIDispatcher>(),
            settings.Object,
            NullLogger<LibraryViewModel>.Instance);

        await viewModel.SetAsFocusCommand.ExecuteAsync(game);

        settings.Verify(s => s.SetFocusGameIdAsync(7), Times.Once);
        game.Status.Should().Be(GameStatus.Paused);
    }

    [Fact]
    public async Task SetAsFocus_ShowsFocusedGameInLibrary()
    {
        var game = new Game { Id = 7, Name = "Hades II", Status = GameStatus.Paused };

        var gameRepo = new Mock<IGameRepository>();
        gameRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game> { game });
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Games).Returns(gameRepo.Object);

        var viewModel = new LibraryViewModel(
            unitOfWork.Object,
            new GameFocus.Application.Services.GameDiscoveryService(Mock.Of<IUnitOfWork>(), System.Array.Empty<IGameSource>()),
            Mock.Of<IDashboardPublisher>(),
            Mock.Of<IUIDispatcher>(),
            Mock.Of<ISettingsService>(),
            NullLogger<LibraryViewModel>.Instance);

        viewModel.FocusStatusText.Should().Be("Nenhum jogo em foco");

        await viewModel.SetAsFocusCommand.ExecuteAsync(game);

        viewModel.FocusStatusText.Should().Be("Foco: Hades II");
    }

    [Fact]
    public async Task LoadGames_RestoresFocusChosenInPreviousRun()
    {
        var game = new Game { Id = 7, Name = "Hades II", Status = GameStatus.Paused };

        var gameRepo = new Mock<IGameRepository>();
        gameRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game> { game });
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Games).Returns(gameRepo.Object);

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetFocusGameId()).Returns(7);

        var viewModel = new LibraryViewModel(
            unitOfWork.Object,
            new GameFocus.Application.Services.GameDiscoveryService(Mock.Of<IUnitOfWork>(), System.Array.Empty<IGameSource>()),
            Mock.Of<IDashboardPublisher>(),
            Mock.Of<IUIDispatcher>(),
            settings.Object,
            NullLogger<LibraryViewModel>.Instance);

        await viewModel.LoadGamesAsync();

        viewModel.FocusStatusText.Should().Be("Foco: Hades II");
    }
}

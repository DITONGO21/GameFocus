using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using GameFocus.Application.Interfaces;
using GameFocus.Application.Services;
using GameFocus.Application.ViewModels;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameFocus.Tests.Unit;

/// <summary>
/// Pages are recreated on every navigation (no NavigationCache), so each visit builds new ViewModels
/// against the singleton DashboardPublisher. Old ViewModels must stop reacting once their page is gone.
/// </summary>
public class PublisherSubscriptionLifetimeTests
{
    private static (Mock<IGameRepository> games, IUnitOfWork uow) CreateUnitOfWork()
    {
        var gameRepo = new Mock<IGameRepository>();
        gameRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game>());

        var sessionRepo = new Mock<ISessionRepository>();
        sessionRepo.Setup(r => r.GetSessionsInDateRangeAsync(It.IsAny<System.DateTime>(), It.IsAny<System.DateTime>()))
            .ReturnsAsync(new List<GameSession>());
        var goalRepo = new Mock<IGoalRepository>();
        goalRepo.Setup(r => r.GetActiveGoalsAsync()).ReturnsAsync(new List<Goal>());

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.Games).Returns(gameRepo.Object);
        uow.Setup(u => u.Sessions).Returns(sessionRepo.Object);
        uow.Setup(u => u.Goals).Returns(goalRepo.Object);
        return (gameRepo, uow.Object);
    }

    private static IUIDispatcher ImmediateDispatcher()
    {
        var dispatcher = new Mock<IUIDispatcher>();
        dispatcher.Setup(d => d.Enqueue(It.IsAny<System.Action>()))
            .Callback<System.Action>(a => a());
        return dispatcher.Object;
    }

    private static LibraryViewModel CreateLibrary(IUnitOfWork uow, IDashboardPublisher publisher) =>
        new(uow, new GameFocus.Application.Services.GameDiscoveryService(Mock.Of<IUnitOfWork>(), System.Array.Empty<IGameSource>()), publisher, ImmediateDispatcher(), Mock.Of<ISettingsService>(),
            NullLogger<LibraryViewModel>.Instance);

    private static DashboardViewModel CreateDashboard(IUnitOfWork uow, IDashboardPublisher publisher) =>
        new(uow, Mock.Of<IGameSessionService>(), Mock.Of<IDatabaseInitializer>(), publisher, ImmediateDispatcher(),
            null!, Mock.Of<ISettingsService>(), NullLogger<DashboardViewModel>.Instance);

    [Fact]
    public async Task DisposedLibrary_NoLongerReloadsOnPublisherEvent()
    {
        var (games, uow) = CreateUnitOfWork();
        var publisher = new DashboardPublisher();

        var first = CreateLibrary(uow, publisher);
        var second = CreateLibrary(uow, publisher);
        first.Dispose();

        publisher.PublishSessionUpdated(new { Event = "SessionEnded" });

        games.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposedDashboard_NoLongerReloadsOnPublisherEvent()
    {
        var (games, uow) = CreateUnitOfWork();
        var publisher = new DashboardPublisher();

        var first = CreateDashboard(uow, publisher);
        var second = CreateDashboard(uow, publisher);
        first.Dispose();

        publisher.PublishSessionUpdated(new { Event = "SessionEnded" });

        games.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task RecreatingPages_WithDisposal_ProducesOneReloadPerEvent()
    {
        var (games, uow) = CreateUnitOfWork();
        var publisher = new DashboardPublisher();

        // Simulate five visits to the Library page, each one disposed when the page unloads.
        for (var visit = 0; visit < 5; visit++)
        {
            using var page = CreateLibrary(uow, publisher);
        }

        publisher.PublishSessionUpdated(new { Event = "SessionStarted" });

        games.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var (_, uow) = CreateUnitOfWork();
        var vm = CreateLibrary(uow, new DashboardPublisher());

        vm.Dispose();
        var act = () => vm.Dispose();

        act.Should().NotThrow();
    }
}

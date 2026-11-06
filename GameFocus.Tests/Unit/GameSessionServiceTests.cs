using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameFocus.Services.Monitoring;
using GameFocus.Application.Interfaces;
using Moq;
using Xunit;

namespace GameFocus.Tests.Unit;

public class GameSessionServiceTests
{
    private Mock<IGameProcessMonitor> CreateProcessMonitorMock(HashSet<string>? runningProcesses = null)
    {
        var mock = new Mock<IGameProcessMonitor>();
        mock.Setup(m => m.GetRunningProcesses(It.IsAny<IEnumerable<string>>()))
            .Returns(runningProcesses ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return mock;
    }

    [Fact]
    public void UpdateWatchList_SetsProcessNames()
    {
        // Arrange
        var mockMonitor = CreateProcessMonitorMock();
        var service = new GameSessionService(mockMonitor.Object);
        var names = new List<string> { "notepad", "calc" };

        // Act
        service.UpdateWatchList(names);

        // Assert — no exception, service is ready
        service.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public void StartMonitoring_SetsIsMonitoringTrue()
    {
        // Arrange
        var mockMonitor = CreateProcessMonitorMock();
        var service = new GameSessionService(mockMonitor.Object);
        service.UpdateWatchList(new[] { "notepad" });

        // Act
        service.StartMonitoring();

        // Assert
        service.IsMonitoring.Should().BeTrue();

        // Cleanup
        service.StopMonitoring();
        service.Dispose();
    }

    [Fact]
    public void StopMonitoring_SetsIsMonitoringFalse()
    {
        // Arrange
        var mockMonitor = CreateProcessMonitorMock();
        var service = new GameSessionService(mockMonitor.Object);
        service.StartMonitoring();

        // Act
        service.StopMonitoring();

        // Assert
        service.IsMonitoring.Should().BeFalse();
        service.Dispose();
    }

    [Fact]
    public void StartMonitoring_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var mockMonitor = CreateProcessMonitorMock();
        var service = new GameSessionService(mockMonitor.Object);

        // Act
        service.StartMonitoring();
        service.StartMonitoring(); // should be idempotent

        // Assert
        service.IsMonitoring.Should().BeTrue();

        service.StopMonitoring();
        service.Dispose();
    }

    [Fact]
    public async Task OnGameStarted_FiredWhenWatchedProcessDetected()
    {
        // Arrange: mock process monitor to return "dotnet" as running
        var mockMonitor = CreateProcessMonitorMock(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dotnet" });
        var service = new GameSessionService(mockMonitor.Object);
        // speed up test
        service.SetPollingInterval(100);
        
        var detected = new List<string>();

        service.OnGameStarted += (sender, name) => detected.Add(name);

        service.UpdateWatchList(new[] { "dotnet" });

        // Act
        service.StartMonitoring();
        await Task.Delay(300); // wait for at least 2 polling cycles
        service.StopMonitoring();

        // Assert
        detected.Should().Contain("dotnet", "dotnet process should be detected since it was mocked as running");
        service.Dispose();
    }

    [Fact]
    public async Task OnGameStarted_NotFiredForNonRunningProcess()
    {
        // Arrange
        var mockMonitor = CreateProcessMonitorMock(); // returns empty
        var service = new GameSessionService(mockMonitor.Object);
        service.SetPollingInterval(100);
        var detected = new List<string>();

        service.OnGameStarted += (sender, name) => detected.Add(name);
        service.UpdateWatchList(new[] { "this_process_does_not_exist_xyz_12345" });

        // Act
        service.StartMonitoring();
        await Task.Delay(300);
        service.StopMonitoring();

        // Assert
        detected.Should().BeEmpty();
        service.Dispose();
    }

    [Fact]
    public void UpdateWatchList_PreservesSpacesAndMatchesProcessNamesWithSpaces()
    {
        // Arrange
        var service = new GameSessionService(CreateProcessMonitorMock().Object);

        // Act
        service.UpdateWatchList(new[] { "Hollow Knight Silksong" });

        // Assert
        var watchList = (HashSet<string>)typeof(GameSessionService)
            .GetField("_watchList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(service)!;

        watchList.Should().Contain("Hollow Knight Silksong");

        var normalized = (string)typeof(GameSessionService)
            .GetMethod("NormalizeProcessName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object?[] { "Hollow Knight Silksong" })!;

        normalized.Should().Be("Hollow Knight Silksong");
    }

    [Fact]
    public void UpdateWatchList_IgnoresEmptyAndWhitespace()
    {
        // Arrange
        var mockMonitor = CreateProcessMonitorMock();
        var service = new GameSessionService(mockMonitor.Object);

        // Act
        service.UpdateWatchList(new[] { "", " ", "notepad", null! });

        // Assert — no crash
        service.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public void Dispose_StopsMonitoring()
    {
        // Arrange
        var mockMonitor = CreateProcessMonitorMock();
        var service = new GameSessionService(mockMonitor.Object);
        service.StartMonitoring();

        // Act
        service.Dispose();

        // Assert
        service.IsMonitoring.Should().BeFalse();
    }
}

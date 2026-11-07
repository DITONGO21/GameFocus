using System;
using System.Collections.Generic;
using System.Linq;
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

public class GoalsAndClockTests
{
    private static DateTime Day(int year, int month, int day) => new(year, month, day, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public void LongestConsecutiveDays_CountsLongestRunOfCalendarDays()
    {
        var sessions = new[]
        {
            Day(2026, 1, 1), Day(2026, 1, 2), Day(2026, 1, 3), // run of 3
            Day(2026, 1, 5), Day(2026, 1, 6),                  // run of 2
            Day(2026, 1, 2)                                    // same day as before: not counted twice
        }.Select(d => d.ToUniversalTime());

        GoalService.LongestConsecutiveDays(sessions).Should().Be(3);
    }

    [Fact]
    public void LongestConsecutiveDays_IsZeroWithoutSessions()
    {
        GoalService.LongestConsecutiveDays(Array.Empty<DateTime>()).Should().Be(0);
    }

    [Fact]
    public void ManualProgress_CompletesGoalWhenTargetIsReachedAndNeverExceedsIt()
    {
        var goal = new Goal { Type = GoalType.Achievements, TargetValue = 2, CurrentValue = 1 };

        GoalService.AddManualProgress(goal);
        goal.IsCompleted.Should().BeTrue();

        GoalService.AddManualProgress(goal);
        goal.CurrentValue.Should().Be(2);
    }

    [Fact]
    public async Task RankGoal_StoresRankNameAsTargetAndIsCompletedByHand()
    {
        var goals = new List<Goal>();
        var goalRepo = new Mock<IGoalRepository>();
        goalRepo.Setup(r => r.AddAsync(It.IsAny<Goal>())).Callback<Goal>(goals.Add).Returns(Task.CompletedTask);
        goalRepo.Setup(r => r.GetActiveGoalsAsync()).ReturnsAsync(() => goals.ToList());
        var gameRepo = new Mock<IGameRepository>();
        gameRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Game> { new() { Id = 1, Name = "CS2" } });

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Goals).Returns(goalRepo.Object);
        unitOfWork.Setup(u => u.Games).Returns(gameRepo.Object);

        var vm = new GoalsViewModel(unitOfWork.Object, Mock.Of<IGoalService>());
        vm.Games.Add(new Game { Id = 1, Name = "CS2" });
        vm.SelectedGameForGoal = vm.Games.Single();
        vm.NewGoalTitle = "Reach Diamond";
        vm.SelectedGoalTypeIndex = (int)GoalType.RankUp;
        vm.NewGoalTargetLabel = " Diamond ";

        await vm.AddGoalCommand.ExecuteAsync(null);

        var created = goals.Single();
        created.TargetLabel.Should().Be("Diamond");
        created.TargetValue.Should().Be(1);
        created.IsCompleted.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(61, "00:01:01")]
    [InlineData(3723, "01:02:03")]
    [InlineData(90061, "25:01:01")]
    [InlineData(-5, "00:00:00")]
    public void FormatElapsed_ShowsHoursMinutesSeconds(double seconds, string expected)
    {
        DashboardViewModel.FormatElapsed(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
    }
}

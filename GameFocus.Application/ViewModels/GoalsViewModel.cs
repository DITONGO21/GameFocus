using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Application.Interfaces;

namespace GameFocus.Application.ViewModels;

public partial class GoalsViewModel : BaseViewModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGoalService _goalService;

    [ObservableProperty]
    private ObservableCollection<Goal> _goals = new();

    [ObservableProperty]
    private ObservableCollection<Game> _games = new();

    [ObservableProperty]
    private Goal? _selectedGoal;

    [ObservableProperty]
    private Game? _selectedGameForGoal;

    [ObservableProperty]
    private string _newGoalTitle = string.Empty;

    [ObservableProperty]
    private int _selectedGoalTypeIndex;

    [ObservableProperty]
    private double _newGoalTargetValue = 60;

    [ObservableProperty]
    private bool _hasGoals;

    [ObservableProperty]
    private bool _hasGames;

    public GoalsViewModel(IUnitOfWork unitOfWork, IGoalService goalService)
    {
        _unitOfWork = unitOfWork;
        _goalService = goalService;
        Title = "Goals";
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            // Load active goals
            var goals = (await _unitOfWork.Goals.GetActiveGoalsAsync()).ToList();
            Goals.Clear();
            foreach (var goal in goals)
            {
                Goals.Add(goal);
            }
            HasGoals = Goals.Count > 0;

            // Load games for the dropdown
            var games = (await _unitOfWork.Games.GetAllAsync()).ToList();
            Games.Clear();
            foreach (var game in games)
            {
                Games.Add(game);
            }
            HasGames = Games.Count > 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task AddGoalAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGoalTitle) || SelectedGameForGoal == null) return;

        var goalType = (GoalType)SelectedGoalTypeIndex;

        var goal = new Goal
        {
            Title = NewGoalTitle.Trim(),
            GameId = SelectedGameForGoal.Id,
            Type = goalType,
            TargetValue = NewGoalTargetValue,
            CurrentValue = 0,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Goals.AddAsync(goal);
        await _unitOfWork.SaveChangesAsync();

        // Start from the play time already persisted for the game (e.g. 30/60 min, not 0/60).
        await _goalService.RecalculateGoalsAsync(goal.GameId);

        NewGoalTitle = string.Empty;
        NewGoalTargetValue = 60;

        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task DeleteGoalAsync(Goal? goal)
    {
        if (goal == null) return;
        _unitOfWork.Goals.Delete(goal);
        await _unitOfWork.SaveChangesAsync();
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task CompleteGoalAsync(Goal? goal)
    {
        if (goal == null) return;
        goal.IsCompleted = true;
        goal.CurrentValue = goal.TargetValue;
        _unitOfWork.Goals.Update(goal);
        await _unitOfWork.SaveChangesAsync();
        await LoadDataAsync();
    }

    public static double GetProgressPercentage(Goal goal)
    {
        if (goal.TargetValue <= 0) return 100;
        return Math.Min((goal.CurrentValue / goal.TargetValue) * 100.0, 100.0);
    }
}

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Application.Services;

namespace GameFocus.Application.ViewModels;

public partial class CoachViewModel : BaseViewModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly GameFocusCoachService _coachService;

    [ObservableProperty]
    private HabitReport? _latestReport;

    [ObservableProperty]
    private ObservableCollection<HabitReport> _allReports = new();

    [ObservableProperty]
    private string _suggestion = "There are no reports yet. Click 'Generate report' to analyse your gaming habits!";

    [ObservableProperty]
    private string _reportText = string.Empty;

    [ObservableProperty]
    private int _totalGamesPlayed;

    [ObservableProperty]
    private int _gameSwitches;

    [ObservableProperty]
    private double _averageSessionMinutes;

    [ObservableProperty]
    private string _mainGameName = "—";

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _hasReports;

    public CoachViewModel(IUnitOfWork unitOfWork, GameFocusCoachService coachService)
    {
        _unitOfWork = unitOfWork;
        _coachService = coachService;
        Title = "GameFocus Coach";
    }

    [RelayCommand]
    public async Task LoadReportsAsync()
    {
        IsBusy = true;
        try
        {
            var reports = (await _unitOfWork.HabitReports.GetAllAsync()).ToList();
            AllReports.Clear();
            foreach (var report in reports)
            {
                AllReports.Add(report);
            }

            LatestReport = reports.FirstOrDefault();
            HasReports = reports.Count > 0;

            if (LatestReport != null)
            {
                Suggestion = LatestReport.Suggestion;
                ReportText = LatestReport.ReportText;
                TotalGamesPlayed = LatestReport.TotalGamesPlayed;
                GameSwitches = LatestReport.GameSwitches;
                AverageSessionMinutes = Math.Round(LatestReport.AverageSessionDurationMinutes, 1);
                MainGameName = LatestReport.MainGame?.Name ?? "—";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task GenerateReportAsync()
    {
        IsGenerating = true;
        try
        {
            var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek + 1);
            await _coachService.GenerateWeeklyReportAsync(weekStart);
            await LoadReportsAsync();
        }
        finally
        {
            IsGenerating = false;
        }
    }
}

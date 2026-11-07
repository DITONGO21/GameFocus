using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Application.Interfaces;
using Microsoft.Extensions.Logging;
namespace GameFocus.Application.ViewModels;

public partial class DashboardViewModel : BaseViewModel, IDisposable
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGameSessionService _sessionService;
    private readonly IDatabaseInitializer _dbInitializer;
    private readonly IDashboardPublisher _publisher;
    private readonly IUIDispatcher _dispatcher;
    private readonly IGamingPatternAnalyzer _analyzer;
    private readonly ISettingsService _settingsService;
    private bool _disposed;
        private readonly Microsoft.Extensions.Logging.ILogger<DashboardViewModel>? _logger;

    [ObservableProperty]
    private Game? _mainGame;

    [ObservableProperty]
    private string _mainGameName = "No game selected";

    [ObservableProperty]
    private string _mainGameStatus = string.Empty;

    // Session clock: the game being played right now and how long the current session has run.
    [ObservableProperty]
    private string _activeGameName = string.Empty;

    [ObservableProperty]
    private string _elapsedText = "Not playing right now";

    private DateTime? _activeSessionStartUtc;

    [ObservableProperty]
    private Goal? _currentGoal;

    [ObservableProperty]
    private double _weeklyHours;

    [ObservableProperty]
    private string _weeklyHoursText = "0h";

    [ObservableProperty]
    private int _consecutiveDays;

    [ObservableProperty]
    private string _coachSuggestion = "There are no reports yet. Play a little, then generate your first report on the Coach page!";

    [ObservableProperty]
    private int _totalGamesCount;

    [ObservableProperty]
    private int _activeGoalsCount;

    [ObservableProperty]
    private ObservableCollection<GameSession> _recentSessions = new();

    public DashboardViewModel(IUnitOfWork unitOfWork, IGameSessionService sessionService, IDatabaseInitializer dbInitializer, IDashboardPublisher publisher, IUIDispatcher dispatcher, IGamingPatternAnalyzer analyzer, ISettingsService settingsService, Microsoft.Extensions.Logging.ILogger<DashboardViewModel>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _sessionService = sessionService;
        _dbInitializer = dbInitializer;
        _publisher = publisher;
        _dispatcher = dispatcher;
        _analyzer = analyzer;
        _settingsService = settingsService;
            _logger = logger;
        Title = "Dashboard";

        _publisher.OnSessionUpdated += Publisher_OnSessionUpdated;
    }

    // Called every second by the page while it is visible. Pure calculation from the session start.
    public void UpdateElapsed(DateTime utcNow)
    {
        ElapsedText = _activeSessionStartUtc.HasValue
            ? FormatElapsed(utcNow - _activeSessionStartUtc.Value)
            : "Not playing right now";
    }

    // hh:mm:ss, with hours that can go past 24.
    public static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private void Publisher_OnSessionUpdated(object? sender, object e)
    {
        _dispatcher.Enqueue(() =>
        {
            if (!_disposed) _ = LoadDataAsync();
        });
    }

    // Called when the page leaves the frame. The publisher is a singleton, so without this
    // the ViewModel would stay subscribed and keep reloading after its page is gone.
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _publisher.OnSessionUpdated -= Publisher_OnSessionUpdated;
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            // Ensure database exists
            await _dbInitializer.EnsureDatabaseCreatedAsync();

            // Load games
            var games = (await _unitOfWork.Games.GetAllAsync()).ToList();
            TotalGamesCount = games.Count;

            // Main focus game: the one chosen in the library, otherwise the game currently being played
            var focusGameId = _settingsService.GetFocusGameId();
            var focusGame = games.FirstOrDefault(g => g.Id == focusGameId)
                            ?? games.FirstOrDefault(g => g.Status == GameStatus.Playing);
            MainGame = focusGame;
            MainGameName = focusGame?.Name ?? "No game selected";
            // Live state from the session lifecycle, so the card never implies a game is running when it is not
            MainGameStatus = focusGame == null
                ? string.Empty
                : focusGame.Status == GameStatus.Playing ? "Playing now" : "Paused";

            // Session clock: the session that is running right now, if any.
            var activeSession = await _unitOfWork.Sessions.GetActiveSessionAsync();
            _activeSessionStartUtc = activeSession == null ? null : DateTime.SpecifyKind(activeSession.StartTimeUtc, DateTimeKind.Utc);
            ActiveGameName = activeSession?.Game?.Name ?? string.Empty;
            UpdateElapsed(DateTime.UtcNow);

            // Calculate weekly hours
            var weekStart = DateTime.UtcNow.AddDays(-7);
            var sessions = (await _unitOfWork.Sessions.GetSessionsInDateRangeAsync(weekStart, DateTime.UtcNow)).ToList();
            WeeklyHours = Math.Round(sessions.Sum(s => s.DurationSeconds) / 3600.0, 1);
            WeeklyHoursText = $"{WeeklyHours}h";

            // Active goals
            var activeGoals = (await _unitOfWork.Goals.GetActiveGoalsAsync()).ToList();
            ActiveGoalsCount = activeGoals.Count;
            CurrentGoal = activeGoals.FirstOrDefault();

            // Recent sessions (last 5)
            RecentSessions.Clear();
            foreach (var session in sessions.OrderByDescending(s => s.StartTimeUtc).Take(5))
            {
                RecentSessions.Add(session);
            }

            // Coach suggestion
            if (_analyzer != null)
            {
                var globalPatterns = await _analyzer.AnalyzeGlobalPatternsAsync();
                if (globalPatterns != null && globalPatterns.Count > 0)
                {
                    // Most confident pattern first
                    var best = globalPatterns.OrderByDescending(p => p.Confidence).First();
                    CoachSuggestion = $"Insight: {best.Description}";
                }
                else
                {
                    CoachSuggestion = "Play a little more to unlock patterns and insights.";
                }
            }
            else
            {
                var report = await _unitOfWork.HabitReports.GetLatestReportAsync();
                if (report != null)
                {
                    CoachSuggestion = report.Suggestion;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading dashboard data");
            CoachSuggestion = "Welcome to GameFocus! Add games to the Library to get started.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

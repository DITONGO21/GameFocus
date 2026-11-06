using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using GameFocus.Application.Interfaces;
using GameFocus.Application.Services;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace GameFocus.Application.ViewModels;

public partial class LibraryViewModel : BaseViewModel, IDisposable
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly GameDiscoveryService _discovery;
    private readonly IDashboardPublisher _publisher;
    private readonly IUIDispatcher _dispatcher;
    private readonly ISettingsService _settingsService;
    private readonly Microsoft.Extensions.Logging.ILogger<LibraryViewModel>? _logger;
    private readonly SemaphoreSlim _loadGamesGate = new(1, 1);
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<Game> _games = new();

    [ObservableProperty]
    private Game? _selectedGame;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _hasGames;

    [ObservableProperty]
    private string _focusStatusText = "Nenhum jogo em foco";

    public LibraryViewModel(IUnitOfWork unitOfWork, GameDiscoveryService discovery, IDashboardPublisher publisher, IUIDispatcher dispatcher, ISettingsService settingsService, Microsoft.Extensions.Logging.ILogger<LibraryViewModel>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _discovery = discovery;
        _publisher = publisher;
        _dispatcher = dispatcher;
        _settingsService = settingsService;
        _logger = logger;
        Title = "Library";

        _publisher.OnSessionUpdated += Publisher_OnSessionUpdated;
    }

    private void Publisher_OnSessionUpdated(object? sender, object payload)
    {
        _dispatcher.Enqueue(() =>
        {
            if (!_disposed) _ = LoadGamesAsync();
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
    public async Task LoadGamesAsync()
    {
        await _loadGamesGate.WaitAsync();
        IsBusy = true;
        try
        {
            var games = (await _unitOfWork.Games.GetAllAsync()).ToList();
            Games.Clear();
            foreach (var game in games)
            {
                Games.Add(game);
            }
            HasGames = Games.Count > 0;
            UpdateFocusStatus(games, _settingsService.GetFocusGameId());
        }
        finally
        {
            IsBusy = false;
            _loadGamesGate.Release();
        }
    }

    [RelayCommand]
    public async Task PauseAllAsync()
    {
        _logger?.LogInformation("Pausing all games via PauseAll command");
        var all = (await _unitOfWork.Games.GetAllAsync()).ToList();
        foreach (var g in all)
        {
            if (g.Status != GameStatus.Paused)
            {
                g.Status = GameStatus.Paused;
                _unitOfWork.Games.Update(g);
            }
        }
        await _unitOfWork.SaveChangesAsync();
        await LoadGamesAsync();
    }

    [RelayCommand]
    public async Task ImportSteamGamesAsync()
    {
        IsBusy = true;
        try
        {
            _logger?.LogInformation("Starting game import from all sources");
            var added = await _discovery.ImportFromSourcesAsync();
            _logger?.LogInformation("Game import finished: {Count} new games", added);
            await LoadGamesAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LaunchGameAsync(Game? game)
    {
        if (game == null) return;

        try
        {
            _logger?.LogInformation("Launching game {GameName} (SteamAppId={SteamId})", game.Name, game.SteamAppId);
            // Prefer Steam protocol if we have an AppId
            if (game.SteamAppId.HasValue)
            {
                var steamUri = $"steam://rungameid/{game.SteamAppId.Value}";
                Process.Start(new ProcessStartInfo(steamUri) { UseShellExecute = true });
            }
            else if (!string.IsNullOrWhiteSpace(game.InstalledPath))
            {
                // If InstalledPath points directly to an exe, run it
                if (File.Exists(game.InstalledPath))
                {
                    Process.Start(new ProcessStartInfo(game.InstalledPath) { UseShellExecute = true });
                }
                else if (Directory.Exists(game.InstalledPath))
                {
                    // Try to find an exe in the folder matching the folder name or first exe
                    var dir = new DirectoryInfo(game.InstalledPath);
                    var exe = dir.GetFiles("*.exe").FirstOrDefault(f => string.Equals(f.Name, dir.Name + ".exe", StringComparison.OrdinalIgnoreCase))
                              ?? dir.GetFiles("*.exe").FirstOrDefault();
                    if (exe != null)
                    {
                        Process.Start(new ProcessStartInfo(exe.FullName) { UseShellExecute = true });
                    }
                    else
                    {
                        // open folder if no exe found
                        Process.Start(new ProcessStartInfo(game.InstalledPath) { UseShellExecute = true });
                    }
                }
            }

            // Do not mutate the persisted Game status here. The active session lifecycle must be owned
            // by the process monitor and SessionCoordinator so the database has a single source of truth.
            _logger?.LogInformation("Game {GameName} launch requested; status will be updated by runtime session detection", game.Name);
        }
        catch
        {
            _logger?.LogWarning("Failed to launch game {GameName}", game?.Name);
        }
    }

    // Returns null on success, or the message to show the user when the input is invalid.
    public async Task<string?> AddExecutableGameAsync(string executablePath, string name, string? developer)
    {
        try
        {
            await _discovery.AddExecutableGameAsync(executablePath, name, developer);
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }

        await LoadGamesAsync();
        return null;
    }

    // Returns null on success, or the message to show the user when the input is invalid.
    public async Task<string?> UpdateManualGameAsync(int gameId, string name, string executablePath)
    {
        try
        {
            await _discovery.UpdateManualGameAsync(gameId, name, executablePath);
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }

        await LoadGamesAsync();
        return null;
    }

    [RelayCommand]
    public async Task DeleteGameAsync(Game? game)
    {
        if (game == null) return;
        _unitOfWork.Games.Delete(game);
        await _unitOfWork.SaveChangesAsync();
        await LoadGamesAsync();
    }

    [RelayCommand]
    public async Task SetAsFocusAsync(Game? game)
    {
        if (game == null) return;

        // Definir foco guarda apenas o jogo principal escolhido pelo utilizador (Dashboard "Jogo Foco Atual").
        // Não altera Game.Status: Playing/Paused continua a ser determinado pela sessão ativa e pelo monitor de processos.
        _logger?.LogInformation("Setting focus game to {GameName} (Id={GameId})", game.Name, game.Id);
        await _settingsService.SetFocusGameIdAsync(game.Id);
        FocusStatusText = $"Foco: {game.Name}";
    }

    private void UpdateFocusStatus(System.Collections.Generic.IEnumerable<Game> games, int? focusGameId)
    {
        var focusGame = games.FirstOrDefault(g => g.Id == focusGameId);
        FocusStatusText = focusGame == null ? "Nenhum jogo em foco" : $"Foco: {focusGame.Name}";
    }

    partial void OnSearchQueryChanged(string value)
    {
        // Simple client-side filter
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = LoadGamesAsync();
            return;
        }

        var filtered = Games.Where(g => g.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList();
        Games.Clear();
        foreach (var game in filtered)
        {
            Games.Add(game);
        }
        HasGames = Games.Count > 0;
    }
}

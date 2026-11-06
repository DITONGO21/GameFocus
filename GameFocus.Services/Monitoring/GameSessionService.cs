using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameFocus.Services.Monitoring;

public class GameSessionService : IGameSessionService, IDisposable
{
    private const int RequiredMissesBeforeClose = 3;

    private bool _isMonitoring;
    private readonly HashSet<string> _watchList = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;
    private readonly HashSet<string> _runningGames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _consecutiveMisses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<GameSessionService>? _logger;
    private readonly IGameProcessMonitor _processMonitor;
    private int _pollingIntervalMs = 3000;

    private static string NormalizeProcessName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4];

        var collapsed = string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Trim();
    }

    private static string CanonicalProcessName(string? value)
    {
        return new string((NormalizeProcessName(value) ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool ProcessNamesMatch(string? left, string? right)
    {
        var leftNormalized = NormalizeProcessName(left);
        var rightNormalized = NormalizeProcessName(right);

        if (string.Equals(leftNormalized, rightNormalized, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(CanonicalProcessName(leftNormalized), CanonicalProcessName(rightNormalized), StringComparison.OrdinalIgnoreCase);
    }

    public GameSessionService(IGameProcessMonitor processMonitor, ILogger<GameSessionService>? logger = null)
    {
        _processMonitor = processMonitor;
        _logger = logger;
    }

    public bool IsMonitoring => _isMonitoring;

    public event EventHandler<string>? OnGameStarted;
    public event EventHandler<string>? OnGameClosed;
    public event EventHandler<string>? OnUnauthorizedGameDetected;

    public void UpdateWatchList(IEnumerable<string> processNames)
    {
        lock (_watchList)
        {
            _watchList.Clear();
            foreach (var name in processNames)
            {
                var normalized = NormalizeProcessName(name);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    _watchList.Add(normalized);
                }
            }
        }

        _logger?.LogInformation("Watchlist updated. Tracking {Count} games.", _watchList.Count);
    }

    public void SetPollingInterval(int milliseconds)
    {
        _pollingIntervalMs = Math.Max(1000, milliseconds);
    }

    public void StartMonitoring()
    {
        if (_isMonitoring) return;

        _logger?.LogInformation("Starting game session monitoring with polling interval {Interval}ms", _pollingIntervalMs);
        _isMonitoring = true;
        _cancellationTokenSource = new CancellationTokenSource();
        _monitoringTask = Task.Run(() => MonitoringLoopAsync(_cancellationTokenSource.Token));
    }

    public void StopMonitoring()
    {
        if (!_isMonitoring) return;

        _logger?.LogInformation("Stopping game session monitoring");
        _isMonitoring = false;

        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error cancelling monitoring token");
        }
    }

    private async Task MonitoringLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            List<string> currentWatchList;
            lock (_watchList)
            {
                currentWatchList = _watchList.ToList();
            }

            if (currentWatchList.Count == 0)
            {
                _logger?.LogInformation("[SESSION MONITOR] No watched games in currentWatchList.");
                await DelayAsync(token);
                continue;
            }

            var currentProcesses = _processMonitor.GetRunningProcesses(currentWatchList)
                .Select(NormalizeProcessName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _logger?.LogInformation("[SESSION MONITOR] Watchlist={WatchList}; CurrentProcesses={CurrentProcesses}; _runningGames={RunningGames}",
                string.Join(", ", currentWatchList),
                currentProcesses.Count == 0 ? "<empty>" : string.Join(", ", currentProcesses),
                _runningGames.Count == 0 ? "<empty>" : string.Join(", ", _runningGames));

            foreach (var gameName in currentWatchList)
            {
                var normalizedGameName = NormalizeProcessName(gameName);
                if (string.IsNullOrWhiteSpace(normalizedGameName))
                {
                    continue;
                }

                var isRunning = currentProcesses.Any(current => ProcessNamesMatch(current, normalizedGameName));
                var wasRunning = _runningGames.Any(running => ProcessNamesMatch(running, normalizedGameName));

                _logger?.LogInformation("[SESSION MONITOR] GameName={GameName}; Normalized={Normalized}; isRunning={IsRunning}; _runningGames.Contains={Contained}",
                    gameName,
                    normalizedGameName,
                    isRunning,
                    wasRunning);

                if (isRunning && !wasRunning)
                {
                    _consecutiveMisses.Remove(normalizedGameName);
                    _runningGames.Add(normalizedGameName);
                    _logger?.LogInformation("[START DIAGNOSTIC] OnGameStarted about to fire: {Process}", normalizedGameName);
                    _logger?.LogInformation("Detected game started: {Process}", normalizedGameName);
                    OnGameStarted?.Invoke(this, normalizedGameName);
                    _logger?.LogInformation("[START DIAGNOSTIC] OnGameStarted fired: {Process}", normalizedGameName);
                }
                else if (isRunning && wasRunning)
                {
                    _consecutiveMisses.Remove(normalizedGameName);
                    _logger?.LogInformation("[SESSION MONITOR] still running: {GameName}", gameName);
                }
                else if (!isRunning && wasRunning)
                {
                    var misses = _consecutiveMisses.TryGetValue(normalizedGameName, out var previousMisses)
                        ? previousMisses + 1
                        : 1;

                    _consecutiveMisses[normalizedGameName] = misses;
                    _logger?.LogInformation("[SESSION MONITOR] Miss count for {GameName}: {Misses}/{Required}", normalizedGameName, misses, RequiredMissesBeforeClose);

                    if (misses >= RequiredMissesBeforeClose)
                    {
                        _consecutiveMisses.Remove(normalizedGameName);
                        _runningGames.Remove(normalizedGameName);
                        _logger?.LogInformation("[SESSION MONITOR] OnGameClosed about to fire: {GameName}", normalizedGameName);
                        OnGameClosed?.Invoke(this, normalizedGameName);
                        _logger?.LogInformation("[SESSION MONITOR] OnGameClosed fired: {GameName}", normalizedGameName);
                    }
                }
                else
                {
                    _logger?.LogInformation("[SESSION MONITOR] still not running: {GameName}", gameName);
                }
            }

            await DelayAsync(token);
        }
    }

    private async Task DelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(_pollingIntervalMs, token);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Monitoring loop cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error in monitoring loop");
            await Task.Delay(5000, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        StopMonitoring();
        _cancellationTokenSource?.Dispose();
    }
}

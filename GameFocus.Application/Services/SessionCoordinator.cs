using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameFocus.Application.Services;

public class SessionCoordinator : IDisposable
{
    private readonly IGameSessionService _detector;
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;
    private readonly IDashboardPublisher _publisher;
    private readonly ILogger<SessionCoordinator>? _logger;

    // map processName → (sessionId, correlationId)
    private readonly ConcurrentDictionary<string, (int SessionId, Guid CorrelationId)> _active = new();

    // Concurrency protection for START/STOP events
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    public SessionCoordinator(IGameSessionService detector, Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory, IDashboardPublisher publisher, ILogger<SessionCoordinator>? logger = null)
    {
        _detector = detector;
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;

        _logger?.LogInformation("[SESSION COORDINATOR] Registering OnGameStarted subscription.");
        _detector.OnGameStarted += Detector_OnGameStarted;
        _logger?.LogInformation("[SESSION COORDINATOR] Registering OnGameClosed subscription.");
        _detector.OnGameClosed += Detector_OnGameClosed;
    }

    private static string NormalizeProcessName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4];

        return string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
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

    private static IEnumerable<string> GetGameProcessCandidates(Game game)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(game.ExecutableName))
        {
            candidates.Add(game.ExecutableName);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(game.InstalledPath) && game.InstalledPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(System.IO.Path.GetFileNameWithoutExtension(game.InstalledPath));
            }

            if (!string.IsNullOrWhiteSpace(game.Name))
            {
                candidates.Add(game.Name);
            }
        }

        return candidates
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task InitializeAsync()
    {
        try
        {
            using var scopeDi = _scopeFactory.CreateScope();
            var unitOfWork = scopeDi.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var games = (await unitOfWork.Games.GetAllAsync()).ToList();
            var gameNames = games
                .SelectMany(GetGameProcessCandidates)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Fix dangling sessions
            var allSessions = await unitOfWork.Sessions.GetAllAsync();
            var danglingSessions = allSessions.Where(s => s.EndTimeUtc == null && s.Status == SessionStatus.Active).ToList();
            var danglingGameIds = new HashSet<int>();
            foreach (var s in danglingSessions)
            {
                s.EndTimeUtc = s.StartTimeUtc;
                s.DurationSeconds = 0;
                s.Status = SessionStatus.Interrupted;
                unitOfWork.Sessions.Update(s);
                danglingGameIds.Add(s.GameId);
            }
            if (danglingSessions.Any())
            {
                var danglingGames = games.Where(g => danglingGameIds.Contains(g.Id)).ToList();
                foreach (var game in danglingGames)
                {
                    game.Status = GameStatus.Paused;
                    unitOfWork.Games.Update(game);
                }

                await unitOfWork.SaveChangesAsync();
                _logger?.LogInformation("Closed {Count} dangling sessions from previous unexpected shutdown.", danglingSessions.Count);
            }

            // Clear stale persisted Playing states that are not backed by a live active session.
            var activeGameIds = allSessions
                .Where(s => s.EndTimeUtc == null && s.Status == SessionStatus.Active)
                .Select(s => s.GameId)
                .Distinct()
                .ToHashSet();

            var stalePlayingGames = games
                .Where(g => g.Status == GameStatus.Playing && !activeGameIds.Contains(g.Id))
                .ToList();

            foreach (var game in stalePlayingGames)
            {
                game.Status = GameStatus.Paused;
                unitOfWork.Games.Update(game);
            }

            if (stalePlayingGames.Count > 0)
            {
                await unitOfWork.SaveChangesAsync();
                _logger?.LogInformation("Cleared {Count} stale Playing statuses from previous runs.", stalePlayingGames.Count);
            }

            // Recalculate active goals from the persisted play time (best-effort: must not block monitoring).
            try
            {
                var goalService = scopeDi.ServiceProvider.GetService<IGoalService>();
                if (goalService != null)
                {
                    await goalService.RecalculateGoalsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to recalculate goals at startup");
            }

            _detector.UpdateWatchList(gameNames);
            _detector.StartMonitoring();
            _logger?.LogInformation("SessionCoordinator initialized and monitoring started.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize SessionCoordinator");
        }
    }

    private void Detector_OnGameStarted(object? sender, string processName)
    {
        _ = HandleStartAsync(processName);
    }

    private void Detector_OnGameClosed(object? sender, string processName)
    {
        _logger?.LogWarning(
            "[STOP SOURCE] Detector_OnGameClosed → HandleStopAsync({ProcessName})",
            processName);

        _ = HandleStopAsync(processName);
    }

    private async Task HandleStartAsync(string processName)
    {
        await _sessionLock.WaitAsync();
        try
        {
            var correlation = Guid.NewGuid();
            using var scope = _logger?.BeginScope(new System.Collections.Generic.Dictionary<string, object> { ["CorrelationId"] = correlation, ["Service"] = nameof(SessionCoordinator), ["Game"] = processName });
            try
            {
                _logger?.LogInformation("[START DIAGNOSTIC] HandleStartAsync CALLED: GameName={GameName}", processName);
                _logger?.LogInformation("Session start detected for {Game}", processName);

                // If already active, ignore duplicate start
                if (_active.ContainsKey(processName))
                {
                    _logger?.LogDebug("Start event ignored for already active process {Game}", processName);
                    return;
                }

                using var scopeDi = _scopeFactory.CreateScope();
                var unitOfWork = scopeDi.ServiceProvider.GetRequiredService<IUnitOfWork>();

                // Try find existing game by ExecutableName, Name or installed path
                var allGames = (await unitOfWork.Games.GetAllAsync()).ToList();
                var game = allGames.FirstOrDefault(g =>
                    GetGameProcessCandidates(g).Any(candidate => ProcessNamesMatch(candidate, processName)) ||
                    (!string.IsNullOrEmpty(g.InstalledPath) && g.InstalledPath.IndexOf(processName, StringComparison.OrdinalIgnoreCase) >= 0)
                );
                if (game == null)
                {
                    game = new Game { Name = processName, Status = GameStatus.Paused };
                    await unitOfWork.Games.AddAsync(game);
                    await unitOfWork.SaveChangesAsync();
                }

                _logger?.LogInformation("[START DIAGNOSTIC] BEFORE SAVE: GameName={GameName}, GameId={GameId}, GameStatus={GameStatus}", game.Name, game.Id, game.Status);

                game.Status = GameStatus.Playing;
                _logger?.LogInformation("[START DIAGNOSTIC] AFTER SET Playing: GameName={GameName}, GameId={GameId}, GameStatus={GameStatus}", game.Name, game.Id, game.Status);
                unitOfWork.Games.Update(game);

                var session = new GameSession { GameId = game.Id, StartTimeUtc = DateTime.UtcNow, Status = SessionStatus.Active };
                await unitOfWork.Sessions.AddAsync(session);
                await unitOfWork.SaveChangesAsync();
                _logger?.LogInformation("[START DIAGNOSTIC] AFTER SAVE: GameName={GameName}, GameId={GameId}, GameStatus={GameStatus}", game.Name, game.Id, game.Status);

                _active[processName] = (session.Id, correlation);

                _logger?.LogInformation("Session created: SessionId={SessionId}, GameId={GameId}", session.Id, game.Id);

                // notify dashboard
                try
                {
                    _publisher.PublishSessionUpdated(new { Event = "SessionStarted", Game = game.Name, SessionId = session.Id, CorrelationId = correlation });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Dashboard publish failed for SessionStart SessionId={SessionId}", session.Id);
                }

                // Intent popup intentionally disabled: it is a separate user interaction and should not auto-open on every session start.

                // DESATIVADO: Fast Switch automático
                // O sistema permite detectar e suportar processos simultâneos.
                // (Testes que exijam exclusividade foram adaptados).
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling session start for {Game}", processName);
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task HandleStopAsync(string processName)
    {
        await _sessionLock.WaitAsync();
        try
        {
            _logger?.LogWarning(
                "[STOP SOURCE] HandleStopAsync CALLED: {GameName}",
                processName);

            _logger?.LogWarning(
                "[STOP SOURCE] StackTrace:\n{StackTrace}",
                Environment.StackTrace);

            if (!_active.TryRemove(processName, out var info))
            {
                _logger?.LogWarning("Stop received for unknown process {Game}", processName);
                return;
            }

            using var scope = _logger?.BeginScope(new System.Collections.Generic.Dictionary<string, object> { ["CorrelationId"] = info.CorrelationId, ["Service"] = nameof(SessionCoordinator), ["Game"] = processName, ["SessionId"] = info.SessionId });
            try
            {
                _logger?.LogInformation("Session stop detected for {Game}, SessionId={SessionId}", processName, info.SessionId);

                // Ensure the GameStatus changes are persisted and visible immediately.
                _logger?.LogInformation("HandleStopAsync active session resolved: process={Process}, sessionId={SessionId}", processName, info.SessionId);

                using var scopeDi = _scopeFactory.CreateScope();
                var unitOfWork = scopeDi.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var goalService = scopeDi.ServiceProvider.GetService<IGoalService>();
                var coachService = scopeDi.ServiceProvider.GetService<ICoachService>();

                var session = await unitOfWork.Sessions.GetByIdAsync(info.SessionId);
                // No-op change to trigger update (ensure references exist)
                if (session == null)
                {
                    _logger?.LogWarning("Session id {SessionId} not found in DB", info.SessionId);
                    return;
                }

                session.EndTimeUtc = DateTime.UtcNow;
                session.DurationSeconds = (session.EndTimeUtc.Value - session.StartTimeUtc).TotalSeconds;
                session.Status = SessionStatus.Finished;
                unitOfWork.Sessions.Update(session);

                // Update game stats
                var game = await unitOfWork.Games.GetByIdAsync(session.GameId);
                if (game != null)
                {
                    _logger?.LogInformation("[STATUS DIAGNOSTIC] HandleStopAsync BEFORE SAVE: GameId={GameId}, GameName={GameName}, GameStatus={Status}", game.Id, game.Name, game.Status);
                    game.TotalPlayTime = game.TotalPlayTime.Add(TimeSpan.FromSeconds(session.DurationSeconds));
                    game.LastPlayed = session.EndTimeUtc;
                    game.Status = GameStatus.Paused;
                    _logger?.LogInformation("[STATUS DIAGNOSTIC] HandleStopAsync AFTER SET: GameId={GameId}, GameName={GameName}, GameStatus={Status}", game.Id, game.Name, game.Status);
                    unitOfWork.Games.Update(game);
                }

                await unitOfWork.SaveChangesAsync();

                if (game != null)
                {
                    var reloaded = await unitOfWork.Games.GetByIdAsync(game.Id);
                    _logger?.LogInformation("[STATUS DIAGNOSTIC] AFTER SaveChangesAsync: GameId={GameId}, GameName={GameName}, GameStatus={Status}", game.Id, game.Name, reloaded?.Status ?? game.Status);
                }

                _logger?.LogInformation("SESSION STOP SAVED: {GameName}", processName);
                _logger?.LogInformation("Session saved: SessionId={SessionId}, Duration={Duration}s, Status={Status}", session.Id, session.DurationSeconds, session.Status);

                if (game != null)
                {
                    var reloaded = await unitOfWork.Games.GetByIdAsync(game.Id);
                    _logger?.LogInformation("POST-SAVE GAME STATUS: {GameName} Id={GameId} Status={Status}", reloaded?.Name ?? game.Name, game.Id, reloaded?.Status ?? game.Status);
                }

                // Notify dashboard (safe call)
                try
                {
                    _publisher.PublishSessionUpdated(new { Event = "SessionEnded", Game = game?.Name ?? processName, SessionId = session.Id, Duration = session.DurationSeconds, CorrelationId = info.CorrelationId });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Dashboard publish failed for SessionId={SessionId}", session.Id);
                }

                // Update goals (best-effort)
                if (goalService != null)
                {
                    try
                    {
                        await goalService.UpdateGoalsAsync(session);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "GoalService failed for SessionId={SessionId}", session.Id);
                    }
                }

                // Coach analysis (best-effort)
                if (coachService != null)
                {
                    try
                    {
                        await coachService.AnalyzeSessionAsync(session);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "CoachService failed for SessionId={SessionId}", session.Id);
                    }
                }

                // Show FeedbackWindow via UIOverlayService
                var overlayService = scopeDi.ServiceProvider.GetService<GameFocus.Application.Interfaces.IUIOverlayService>();
                if (overlayService != null && session.DurationSeconds > 60) // Only ask if played for more than 1 min
                {
                    try
                    {
                        overlayService.ShowFeedbackOverlay(session.Id, async (sId, enjoyment) =>
                        {
                            using var innerScope = _scopeFactory.CreateScope();
                            var innerUow = innerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                            var newFeedback = new SessionFeedback
                            {
                                SessionId = sId,
                                Enjoyment = enjoyment,
                                WantsToContinueTomorrow = true, // default for now
                                CreatedAtUtc = DateTime.UtcNow
                            };
                            await innerUow.SessionInsights.AddFeedbackAsync(newFeedback);
                            await innerUow.SaveChangesAsync();
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to show FeedbackWindow");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling session stop for {Game}", processName);
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public void Dispose()
    {
        _detector.OnGameStarted -= Detector_OnGameStarted;
        _detector.OnGameClosed -= Detector_OnGameClosed;
    }
}

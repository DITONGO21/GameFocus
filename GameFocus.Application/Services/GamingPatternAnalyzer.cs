using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameFocus.Application.Services;

public class GamingPatternAnalyzer : IGamingPatternAnalyzer
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionInsightRepository _insightRepo;
    private readonly ILogger<GamingPatternAnalyzer>? _logger;

    public GamingPatternAnalyzer(IUnitOfWork unitOfWork, ISessionInsightRepository insightRepo, ILogger<GamingPatternAnalyzer>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _insightRepo = insightRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GamingPattern>> AnalyzePatternsAsync(int gameId)
    {
        var sessions = (await _unitOfWork.Sessions.GetByGameIdAsync(gameId)).ToList();
        var intents = (await _insightRepo.GetIntentsByGameIdAsync(gameId)).ToList();
        var feedbacks = (await _insightRepo.GetFeedbacksByGameIdAsync(gameId)).ToList();

        var patterns = new List<GamingPattern>();

        AnalyzeTimeOfDay(sessions, patterns);
        AnalyzeWeekendVsWeekday(sessions, patterns);
        AnalyzeSessionDuration(sessions, patterns);
        AnalyzeDominantIntention(intents, patterns);
        AnalyzeEnjoyment(feedbacks, patterns);

        _logger?.LogInformation("Analyzed {Count} patterns for GameId={GameId}", patterns.Count, gameId);
        return patterns.AsReadOnly();
    }

    public async Task<IReadOnlyList<GamingPattern>> AnalyzeGlobalPatternsAsync()
    {
        var now = DateTime.UtcNow;
        var sessions = (await _unitOfWork.Sessions.GetSessionsInDateRangeAsync(now.AddDays(-30), now)).ToList();
        var recentFeedbacks = (await _insightRepo.GetRecentFeedbacksAsync(50)).ToList();

        var patterns = new List<GamingPattern>();

        AnalyzeTimeOfDay(sessions, patterns);
        AnalyzeWeekendVsWeekday(sessions, patterns);
        AnalyzeSessionDuration(sessions, patterns);
        AnalyzeGameSwitching(sessions, patterns);
        AnalyzeEnjoyment(recentFeedbacks, patterns);
        AnalyzeEngagementTrend(sessions, patterns);

        _logger?.LogInformation("Analyzed {Count} global patterns", patterns.Count);
        return patterns.AsReadOnly();
    }

    private void AnalyzeTimeOfDay(List<GameSession> sessions, List<GamingPattern> patterns)
    {
        if (sessions.Count < 3) return;

        var hourGroups = sessions.GroupBy(s => s.StartTimeUtc.ToLocalTime().Hour switch
        {
            >= 6 and < 12 => "morning",
            >= 12 and < 18 => "afternoon",
            >= 18 and < 23 => "evening",
            _ => "late night"
        }).OrderByDescending(g => g.Count()).ToList();

        var dominant = hourGroups.First();
        double ratio = (double)dominant.Count() / sessions.Count;

        if (ratio >= 0.5)
        {
            patterns.Add(new GamingPattern
            {
                PatternKey = "time_of_day",
                Description = $"You mostly play in the {dominant.Key}. ({Math.Round(ratio * 100)}% of sessions)",
                Confidence = ratio
            });
        }
    }

    private void AnalyzeWeekendVsWeekday(List<GameSession> sessions, List<GamingPattern> patterns)
    {
        if (sessions.Count < 5) return;

        int weekendCount = sessions.Count(s => s.StartTimeUtc.ToLocalTime().DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        double weekendRatio = (double)weekendCount / sessions.Count;

        if (weekendRatio >= 0.6)
        {
            patterns.Add(new GamingPattern
            {
                PatternKey = "weekend_player",
                Description = $"You play more on weekends. ({Math.Round(weekendRatio * 100)}% of sessions)",
                Confidence = weekendRatio
            });
        }
        else if (weekendRatio <= 0.2)
        {
            patterns.Add(new GamingPattern
            {
                PatternKey = "weekday_player",
                Description = $"You play almost only on weekdays. ({Math.Round((1 - weekendRatio) * 100)}% of sessions)",
                Confidence = 1 - weekendRatio
            });
        }
    }

    private void AnalyzeSessionDuration(List<GameSession> sessions, List<GamingPattern> patterns)
    {
        if (sessions.Count < 3) return;

        double avgSeconds = sessions.Average(s => s.DurationSeconds);
        double avgMinutes = avgSeconds / 60.0;

        if (avgMinutes < 15)
        {
            patterns.Add(new GamingPattern
            {
                PatternKey = "short_sessions",
                Description = $"Your sessions are very short (average: {Math.Round(avgMinutes)} minutes). This may indicate difficulty staying focused.",
                Confidence = Math.Min(1.0, 15.0 / Math.Max(avgMinutes, 1))
            });
        }
        else if (avgMinutes > 180)
        {
            patterns.Add(new GamingPattern
            {
                PatternKey = "long_sessions",
                Description = $"Your sessions are very long (average: {Math.Round(avgMinutes)} minutes). Consider taking regular breaks.",
                Confidence = Math.Min(1.0, avgMinutes / 240.0)
            });
        }
    }

    private void AnalyzeGameSwitching(List<GameSession> sessions, List<GamingPattern> patterns)
    {
        if (sessions.Count < 5) return;

        var ordered = sessions.OrderBy(s => s.StartTimeUtc).ToList();
        int switches = 0;
        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].GameId != ordered[i - 1].GameId)
                switches++;
        }

        double switchRate = (double)switches / (ordered.Count - 1);
        if (switchRate >= 0.6)
        {
            patterns.Add(new GamingPattern
            {
                PatternKey = "game_switcher",
                Description = $"You switch games often ({switches} switches in {ordered.Count} sessions). Try to focus on one main game.",
                Confidence = switchRate
            });
        }
    }

    private void AnalyzeDominantIntention(List<SessionIntent> intents, List<GamingPattern> patterns)
    {
        if (intents.Count < 3) return;

        var dominantGroup = intents.GroupBy(i => i.Intention)
            .OrderByDescending(g => g.Count())
            .First();

        double ratio = (double)dominantGroup.Count() / intents.Count;
        if (ratio >= 0.5)
        {
            string intentLabel = dominantGroup.Key switch
            {
                PlayIntention.Relaxar => "relaxar",
                PlayIntention.Competir => "competir",
                PlayIntention.Treinar => "treinar",
                PlayIntention.PassarTempo => "passar tempo",
                _ => dominantGroup.Key.ToString()
            };

            patterns.Add(new GamingPattern
            {
                PatternKey = "dominant_intention",
                Description = $"Quando jogas, preferes {intentLabel}. ({Math.Round(ratio * 100)}% das vezes)",
                Confidence = ratio
            });
        }
    }

    private void AnalyzeEnjoyment(List<SessionFeedback> feedbacks, List<GamingPattern> patterns)
    {
        if (feedbacks.Count < 3) return;

        int negativeCount = feedbacks.Count(f => f.Enjoyment == SessionEnjoyment.NaoGostei);
        double negativeRatio = (double)negativeCount / feedbacks.Count;

        if (negativeRatio >= 0.4)
        {
            patterns.Add(new GamingPattern
            {
                PatternKey = "low_enjoyment",
                Description = $"Many sessions you did not enjoy ({negativeCount} of {feedbacks.Count}). Maybe it is time to try another game.",
                Confidence = negativeRatio
            });
        }

        int positiveCount = feedbacks.Count(f => f.Enjoyment is SessionEnjoyment.Gostei or SessionEnjoyment.Adorei);
        double positiveRatio = (double)positiveCount / feedbacks.Count;
        if (positiveRatio >= 0.7)
        {
            patterns.Add(new GamingPattern
            {
                PatternKey = "high_enjoyment",
                Description = $"You are really enjoying your sessions! ({Math.Round(positiveRatio * 100)}% positive). Keep it up.",
                Confidence = positiveRatio
            });
        }
    }

    private void AnalyzeEngagementTrend(List<GameSession> sessions, List<GamingPattern> patterns)
    {
        if (sessions.Count < 6) return;

        var ordered = sessions.OrderBy(s => s.StartTimeUtc).ToList();
        int half = ordered.Count / 2;
        double firstHalfAvg = ordered.Take(half).Average(s => s.DurationSeconds);
        double secondHalfAvg = ordered.Skip(half).Average(s => s.DurationSeconds);

        if (firstHalfAvg > 0 && secondHalfAvg < firstHalfAvg * 0.6)
        {
            patterns.Add(new GamingPattern
            {
                PatternKey = "declining_engagement",
                Description = "Your sessions are getting shorter. This may indicate losing interest.",
                Confidence = 1.0 - (secondHalfAvg / firstHalfAvg)
            });
        }
        else if (secondHalfAvg > firstHalfAvg * 1.4)
        {
            patterns.Add(new GamingPattern
            {
                PatternKey = "growing_engagement",
                Description = "Your sessions are getting longer. Your engagement is growing!",
                Confidence = Math.Min(1.0, (secondHalfAvg / firstHalfAvg) - 1.0)
            });
        }
    }
}

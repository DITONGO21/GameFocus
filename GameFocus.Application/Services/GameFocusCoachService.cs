using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;

using GameFocus.Application.Interfaces;

namespace GameFocus.Application.Services;

public class GameFocusCoachService : ICoachService
{
    private readonly IUnitOfWork _unitOfWork;

    public GameFocusCoachService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task AnalyzeSessionAsync(GameSession session)
    {
        // Padrões globais são analisados dinamicamente no Dashboard.
        // Aqui podemos registar uma análise estática diária caso necessário.
        await Task.CompletedTask;
    }

    public async Task<HabitReport> GenerateWeeklyReportAsync(DateTime weekStart)
    {
        var weekEnd = weekStart.AddDays(7);
        var sessions = await _unitOfWork.Sessions.GetSessionsInDateRangeAsync(weekStart, weekEnd);
        
        var sessionsList = sessions.ToList();
        int totalSessions = sessionsList.Count;
        int gameSwitches = CalculateGameSwitches(sessionsList);
        int distinctGames = sessionsList.Select(s => s.GameId).Distinct().Count();
        double avgDuration = totalSessions > 0 ? sessionsList.Average(s => s.DurationSeconds / 60.0) : 0;
        
        var mainGameGroup = sessionsList.GroupBy(s => s.GameId)
                                        .OrderByDescending(g => g.Sum(s => s.DurationSeconds / 60.0))
                                        .FirstOrDefault();
                                        
        var report = new HabitReport
        {
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
            TotalGamesPlayed = distinctGames,
            GameSwitches = gameSwitches,
            AverageSessionDurationMinutes = avgDuration,
            MainGameId = mainGameGroup?.Key,
            MainGameTimeMinutes = mainGameGroup?.Sum(s => s.DurationSeconds / 60.0) ?? 0,
            GeneratedAt = DateTime.UtcNow
        };

        report.ReportText = $"This week you played:\n{distinctGames} different games.\nYou switched games {gameSwitches} times.\nAverage session: {Math.Round(avgDuration)} minutes.";
        
        if (distinctGames > 3)
        {
            report.Suggestion = $"You played {distinctGames} different games. Next week, try to focus on just 1 or 2 main games so you stay consistent and reach your goals more easily.";
        }
        else if (avgDuration < 30 && totalSessions > 0)
        {
            report.Suggestion = "Your sessions are very short. Try to set aside longer blocks of time (about 1 hour) so you can get properly immersed without constant interruptions.";
        }
        else
        {
            report.Suggestion = "You are on a great path! Keep your focus on your main game and you will reach your next goal quickly.";
        }

        await _unitOfWork.HabitReports.AddAsync(report);
        await _unitOfWork.SaveChangesAsync();
        
        return report;
    }

    private int CalculateGameSwitches(List<GameSession> sessions)
    {
        if (sessions.Count <= 1) return 0;
        
        // Sessions should be ordered by StartTimeUtc
        var ordered = sessions.OrderBy(s => s.StartTimeUtc).ToList();
        int switches = 0;
        
        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].GameId != ordered[i - 1].GameId)
            {
                switches++;
            }
        }
        
        return switches;
    }
}

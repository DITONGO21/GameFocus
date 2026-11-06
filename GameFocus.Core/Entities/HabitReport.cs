using System;

namespace GameFocus.Core.Entities;

public class HabitReport
{
    public int Id { get; set; }
    public DateTime WeekStartDate { get; set; }
    public DateTime WeekEndDate { get; set; }
    
    // Stats
    public int TotalGamesPlayed { get; set; }
    public int GameSwitches { get; set; }
    public double AverageSessionDurationMinutes { get; set; }
    
    public int? MainGameId { get; set; }
    public Game? MainGame { get; set; }
    
    public double MainGameTimeMinutes { get; set; }
    
    // Generated Suggestion by GameFocus Coach
    public string Suggestion { get; set; } = string.Empty;
    public string ReportText { get; set; } = string.Empty;
    
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

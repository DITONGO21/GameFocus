using System;

namespace GameFocus.Core.Entities;

public enum SessionStatus
{
    Active = 0,
    Finished = 1,
    Interrupted = 2
}

public class GameSession
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public double DurationSeconds { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    
    // Diário / Reflection
    public string? Notes { get; set; }
    public string? Learned { get; set; }
    public string? Feelings { get; set; }
}

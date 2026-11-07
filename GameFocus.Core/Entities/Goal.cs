using System;

namespace GameFocus.Core.Entities;

public class Goal
{
    public int Id { get; set; }
    
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    
    public string Title { get; set; } = string.Empty;
    public GoalType Type { get; set; }
    
    public double TargetValue { get; set; }
    // Text target for goals such as reaching a rank (for example "Diamond"); null for numeric goals.
    public string? TargetLabel { get; set; }
    public double CurrentValue { get; set; }
    
    public bool IsCompleted { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? Deadline { get; set; }
}

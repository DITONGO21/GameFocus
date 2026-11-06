using System;

namespace GameFocus.Core.Entities;

public enum SessionEnjoyment
{
    NaoGostei = 0,
    Neutro = 1,
    Gostei = 2,
    Adorei = 3
}

public class SessionFeedback
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public GameSession Session { get; set; } = null!;
    
    public SessionEnjoyment Enjoyment { get; set; }
    public bool WantsToContinueTomorrow { get; set; }
    public string? EndReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

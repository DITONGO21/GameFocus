using System;

namespace GameFocus.Core.Entities;

public enum PlayIntention
{
    Relaxar = 0,
    Competir = 1,
    Treinar = 2,
    PassarTempo = 3
}

public class SessionIntent
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public GameSession Session { get; set; } = null!;
    
    public PlayIntention Intention { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

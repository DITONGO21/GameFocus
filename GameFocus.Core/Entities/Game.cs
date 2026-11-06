using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameFocus.Core.Entities;

public class Game : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private GameStatus _status = GameStatus.Paused;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Developer { get; set; }

    // Origin: which source discovered the game and its identifier inside that source
    public GameSourceType Source { get; set; } = GameSourceType.Manual;
    public string? ExternalId { get; set; }

    // Steam Integration
    public long? SteamAppId { get; set; }
    public string? ImagePath { get; set; }
    public string? InstalledPath { get; set; }

    // Manual Configuration
    public string? ExecutableName { get; set; }
    public string? ExecutablePath { get; set; }

    // Library features
    public bool IsFavorite { get; set; }
    public string? Category { get; set; }
    public GameStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
                return;

            _status = value;
            OnPropertyChanged();
        }
    }

    // Stats
    public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;
    public DateTime? LastPlayed { get; set; }

    // Relationships
    public ICollection<GameSession> Sessions { get; set; } = new List<GameSession>();
    public ICollection<Goal> Goals { get; set; } = new List<Goal>();

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

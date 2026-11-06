using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameFocus.Application.Interfaces;

public interface IGameSessionService
{
    bool IsMonitoring { get; }
    void StartMonitoring();
    void StopMonitoring();
    void UpdateWatchList(IEnumerable<string> processNames);
    void SetPollingInterval(int milliseconds);
    
    event EventHandler<string> OnGameStarted;
    event EventHandler<string> OnGameClosed;
    event EventHandler<string> OnUnauthorizedGameDetected;
}

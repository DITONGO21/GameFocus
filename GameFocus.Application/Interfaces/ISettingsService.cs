using System.Threading.Tasks;

namespace GameFocus.Application.Interfaces;

public interface ISettingsService
{
    // Returns the configured Steam install path or null if not set
    string? GetSteamPath();

    // Persist settings (async for file IO)
    Task SetSteamPathAsync(string? path);

    // Game chosen as the main focus game in the library, or null if none was chosen
    int? GetFocusGameId();

    Task SetFocusGameIdAsync(int? gameId);
}

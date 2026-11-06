using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using GameFocus.Core.Entities;

namespace GameFocus.Infrastructure.GameSources;

// Launchers that record their installed games in the Windows registry.
// Each source only reads the keys it knows and skips entries without a usable install folder.

public class GogGameSource : IGameSource
{
    private const string GamesKey = @"SOFTWARE\WOW6432Node\GOG.com\Games";
    private readonly IWindowsRegistry _registry;

    public GogGameSource(IWindowsRegistry registry) => _registry = registry;

    public GameSourceType Source => GameSourceType.Gog;

    public Task<IReadOnlyList<Game>> DiscoverAsync()
    {
        var games = new List<Game>();
        foreach (var id in _registry.GetSubKeyNames(GamesKey))
        {
            var key = $@"{GamesKey}\{id}";
            var name = _registry.GetString(key, "gameName");
            var path = _registry.GetString(key, "path");
            var exe = _registry.GetString(key, "exe");
            if (string.IsNullOrWhiteSpace(name)) continue;

            string? executablePath = null;
            if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(exe))
            {
                var candidate = Path.Combine(path, exe);
                if (File.Exists(candidate)) executablePath = candidate;
            }

            games.Add(new Game
            {
                Name = name,
                Source = GameSourceType.Gog,
                ExternalId = id,
                InstalledPath = string.IsNullOrWhiteSpace(path) ? null : path,
                ExecutablePath = executablePath,
                ExecutableName = executablePath == null ? null : Path.GetFileName(executablePath),
                Status = GameStatus.Paused
            });
        }
        return Task.FromResult<IReadOnlyList<Game>>(games);
    }
}

public class UbisoftGameSource : IGameSource
{
    // The launcher records installs per user (HKCU) and, on some setups, machine-wide (HKLM).
    private static readonly string[] InstallsKeys =
    {
        @"HKCU\SOFTWARE\Ubisoft\Launcher\Installs",
        @"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs"
    };

    private readonly IWindowsRegistry _registry;

    public UbisoftGameSource(IWindowsRegistry registry) => _registry = registry;

    public GameSourceType Source => GameSourceType.Ubisoft;

    public Task<IReadOnlyList<Game>> DiscoverAsync()
    {
        var games = new List<Game>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installsKey in InstallsKeys)
        {
            foreach (var id in _registry.GetSubKeyNames(installsKey))
            {
                if (!seen.Add(id)) continue;

                // Records without an install folder (for example, InstallState only) cannot be launched or matched.
                var installDir = _registry.GetString($@"{installsKey}\{id}", "InstallDir");
                if (string.IsNullOrWhiteSpace(installDir)) continue;

                // The launcher stores no display name, so the install folder name is used.
                var folderName = Path.GetFileName(installDir.TrimEnd('\\', '/'));
                if (string.IsNullOrWhiteSpace(folderName)) continue;

                games.Add(new Game
                {
                    Name = folderName,
                    Source = GameSourceType.Ubisoft,
                    ExternalId = id,
                    InstalledPath = installDir,
                    Status = GameStatus.Paused
                });
            }
        }
        return Task.FromResult<IReadOnlyList<Game>>(games);
    }
}

// Legacy EA/Origin games (EA Games registry key). Newer EA app installs are not read here.
public class EaGameSource : IGameSource
{
    private const string GamesKey = @"SOFTWARE\WOW6432Node\Electronic Arts\EA Games";
    private readonly IWindowsRegistry _registry;

    public EaGameSource(IWindowsRegistry registry) => _registry = registry;

    public GameSourceType Source => GameSourceType.EA;

    public Task<IReadOnlyList<Game>> DiscoverAsync()
    {
        var games = new List<Game>();
        foreach (var name in _registry.GetSubKeyNames(GamesKey))
        {
            var installDir = _registry.GetString($@"{GamesKey}\{name}", "Install Dir");
            if (string.IsNullOrWhiteSpace(installDir)) continue;

            games.Add(new Game
            {
                Name = name,
                Source = GameSourceType.EA,
                ExternalId = name,
                InstalledPath = installDir,
                Status = GameStatus.Paused
            });
        }
        return Task.FromResult<IReadOnlyList<Game>>(games);
    }
}

public class RockstarGameSource : IGameSource
{
    private const string GamesKey = @"SOFTWARE\WOW6432Node\Rockstar Games";
    private readonly IWindowsRegistry _registry;

    public RockstarGameSource(IWindowsRegistry registry) => _registry = registry;

    public GameSourceType Source => GameSourceType.Rockstar;

    // Social Club and the Launcher are Rockstar apps, not games.
    public static bool IsGame(string keyName) =>
        !keyName.Contains("Launcher", StringComparison.OrdinalIgnoreCase) &&
        !keyName.Contains("Social Club", StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<Game>> DiscoverAsync()
    {
        var games = new List<Game>();
        foreach (var name in _registry.GetSubKeyNames(GamesKey).Where(IsGame))
        {
            var installFolder = _registry.GetString($@"{GamesKey}\{name}", "InstallFolder");
            if (string.IsNullOrWhiteSpace(installFolder)) continue;

            games.Add(new Game
            {
                Name = name,
                Source = GameSourceType.Rockstar,
                ExternalId = name,
                InstalledPath = installFolder,
                Status = GameStatus.Paused
            });
        }
        return Task.FromResult<IReadOnlyList<Game>>(games);
    }
}

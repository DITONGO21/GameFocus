using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using GameFocus.Core.Entities;

namespace GameFocus.Infrastructure.GameSources;

// Riot Games: RiotClientInstalls.json lists the install folders of each game (for example
// "C:/Riot Games/League of Legends/" and "C:/Riot Games/VALORANT/live/").
public class RiotGameSource : IGameSource
{
    private readonly string _installsFile;

    public RiotGameSource()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Riot Games", "RiotClientInstalls.json"))
    {
    }

    public RiotGameSource(string installsFile)
    {
        _installsFile = installsFile;
    }

    public GameSourceType Source => GameSourceType.Riot;

    public Task<IReadOnlyList<Game>> DiscoverAsync()
    {
        IReadOnlyList<Game> games = File.Exists(_installsFile)
            ? Parse(File.ReadAllText(_installsFile))
            : new List<Game>();

        return Task.FromResult(games);
    }

    public static IReadOnlyList<Game> Parse(string json)
    {
        var games = new List<Game>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("associated_client", out var clients) ||
                clients.ValueKind != JsonValueKind.Object)
            {
                return games;
            }

            foreach (var entry in clients.EnumerateObject())
            {
                var folder = entry.Name.Trim().TrimEnd('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                if (folder.Length == 0 || !Directory.Exists(folder)) continue;

                var name = Path.GetFileName(folder);
                // VALORANT installs into a "live" sub-folder; the game name is the folder above it.
                if (string.Equals(name, "live", StringComparison.OrdinalIgnoreCase))
                {
                    name = Path.GetFileName(Path.GetDirectoryName(folder) ?? string.Empty);
                }
                if (string.IsNullOrWhiteSpace(name)) continue;

                games.Add(new Game
                {
                    Name = name,
                    Source = GameSourceType.Riot,
                    ExternalId = folder,
                    InstalledPath = folder,
                    Status = GameStatus.Paused
                });
            }
        }
        catch (JsonException)
        {
            return new List<Game>();
        }
        return games;
    }
}

// Xbox / Microsoft Store PC games are installed as one folder per title under XboxGames.
public class XboxGameSource : IGameSource
{
    private readonly string _root;

    public XboxGameSource()
        : this(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "XboxGames"))
    {
    }

    public XboxGameSource(string root)
    {
        _root = root;
    }

    public GameSourceType Source => GameSourceType.Xbox;

    public Task<IReadOnlyList<Game>> DiscoverAsync()
    {
        IReadOnlyList<Game> games = Directory.Exists(_root)
            ? Directory.GetDirectories(_root)
                .Where(IsGameFolder)
                .Select(folder => new Game
                {
                    Name = Path.GetFileName(folder),
                    Source = GameSourceType.Xbox,
                    ExternalId = Path.GetFileName(folder),
                    InstalledPath = folder,
                    Status = GameStatus.Paused
                })
                .ToList()
            : new List<Game>();

        return Task.FromResult(games);
    }

    // A game folder has a Content sub-folder with an executable at its top level. This leaves out
    // save-data folders and launcher apps, which also live under XboxGames.
    private static bool IsGameFolder(string folder)
    {
        var content = Path.Combine(folder, "Content");
        return Directory.Exists(content) && Directory.GetFiles(content, "*.exe").Length > 0;
    }
}

// Hytale installs under %APPDATA%\Hytale\install\<channel>\package\game\latest\Client\HytaleClient.exe.
public class HytaleGameSource : IGameSource
{
    private readonly string _installRoot;

    public HytaleGameSource()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hytale", "install"))
    {
    }

    public HytaleGameSource(string installRoot)
    {
        _installRoot = installRoot;
    }

    public GameSourceType Source => GameSourceType.Hytale;

    public Task<IReadOnlyList<Game>> DiscoverAsync()
    {
        var games = new List<Game>();
        if (Directory.Exists(_installRoot))
        {
            foreach (var channel in Directory.GetDirectories(_installRoot))
            {
                var exe = Path.Combine(channel, "package", "game", "latest", "Client", "HytaleClient.exe");
                if (!File.Exists(exe)) continue;

                var channelName = Path.GetFileName(channel);
                games.Add(new Game
                {
                    Name = "Hytale",
                    Source = GameSourceType.Hytale,
                    ExternalId = channelName,
                    InstalledPath = exe,
                    ExecutablePath = exe,
                    ExecutableName = Path.GetFileName(exe),
                    Status = GameStatus.Paused
                });
            }
        }
        return Task.FromResult<IReadOnlyList<Game>>(games);
    }
}

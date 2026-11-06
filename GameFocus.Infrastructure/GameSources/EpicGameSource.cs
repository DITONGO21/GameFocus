using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using GameFocus.Core.Entities;

namespace GameFocus.Infrastructure.GameSources;

// Epic Games Launcher writes one JSON manifest (*.item) per installed game.
public class EpicGameSource : IGameSource
{
    private readonly string _manifestsFolder;

    public EpicGameSource()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests"))
    {
    }

    public EpicGameSource(string manifestsFolder)
    {
        _manifestsFolder = manifestsFolder;
    }

    public GameSourceType Source => GameSourceType.Epic;

    public Task<IReadOnlyList<Game>> DiscoverAsync()
    {
        IReadOnlyList<Game> games = Directory.Exists(_manifestsFolder)
            ? Directory.GetFiles(_manifestsFolder, "*.item")
                .Select(f => Parse(File.ReadAllText(f)))
                .OfType<Game>()
                .ToList()
            : new List<Game>();

        return Task.FromResult(games);
    }

    // Returns null for manifests that are unreadable, incomplete or missing the id or name.
    public static Game? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var appName = ReadString(root, "AppName");
            var displayName = ReadString(root, "DisplayName");
            if (appName == null || displayName == null) return null;

            if (root.TryGetProperty("bIsIncompleteInstall", out var incomplete) &&
                incomplete.ValueKind == JsonValueKind.True)
            {
                return null;
            }

            var installLocation = ReadString(root, "InstallLocation");
            var launchExecutable = ReadString(root, "LaunchExecutable");
            string? executablePath = null;
            if (installLocation != null && launchExecutable != null)
            {
                var candidate = Path.Combine(installLocation, launchExecutable);
                if (File.Exists(candidate)) executablePath = candidate;
            }

            return new Game
            {
                Name = displayName,
                Source = GameSourceType.Epic,
                ExternalId = appName,
                InstalledPath = installLocation,
                ExecutablePath = executablePath,
                ExecutableName = executablePath == null ? null : Path.GetFileName(executablePath),
                Status = GameStatus.Paused
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string property)
    {
        return root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}

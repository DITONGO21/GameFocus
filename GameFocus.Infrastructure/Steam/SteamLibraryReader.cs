using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using GameFocus.Application.Interfaces;
using Microsoft.Extensions.Logging;
using GameFocus.Core.Entities;
using System.Linq;

namespace GameFocus.Infrastructure.Steam;

public class SteamLibraryReader : ISteamLibraryReader
{
    private readonly ISettingsService? _settingsService;
    private readonly ILogger<SteamLibraryReader>? _logger = null;

    // DI constructor used by the app container
    public SteamLibraryReader(ISettingsService? settingsService, ILogger<SteamLibraryReader>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public Task<bool> IsSteamInstalledAsync()
    {
        var path = GetSteamPath();
        var installed = !string.IsNullOrEmpty(path) && Directory.Exists(path);
        _logger?.LogDebug("IsSteamInstalled check: {Installed} path={Path}", installed, path);
        return Task.FromResult(installed);
    }

    public Task<string?> GetSteamInstallPathAsync()
    {
        var p = GetSteamPath();
        _logger?.LogDebug("GetSteamInstallPathAsync -> {Path}", p);
        return Task.FromResult(p);
    }

    public async Task<IEnumerable<Game>> GetInstalledGamesAsync()
    {
        var games = new List<Game>();
        var steamPath = GetSteamPath();

        if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
        {
            _logger?.LogWarning("Steam path not found: {Path}", steamPath);
            return games;
        }

        _logger?.LogInformation("Scanning Steam installation: {Path}", steamPath);

        var libraryFolders = new List<string>();

        // Always scan the main Steam installation
        libraryFolders.Add(steamPath);

        // Read additional Steam libraries
        var libraryFile = Path.Combine(
            steamPath,
            "steamapps",
            "libraryfolders.vdf");

        if (File.Exists(libraryFile))
        {
            try
            {
                var content = await File.ReadAllTextAsync(libraryFile);

                var parsedPaths = VdfParser.GetLibraryPathsFromVdf(
                    content,
                    steamPath);

                foreach (var path in parsedPaths)
                {
                    if (!string.IsNullOrWhiteSpace(path) &&
                        Directory.Exists(path) &&
                        !libraryFolders.Contains(
                            path,
                            StringComparer.OrdinalIgnoreCase))
                    {
                        libraryFolders.Add(path);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Failed to read Steam libraryfolders.vdf");
            }
        }

        _logger?.LogInformation(
            "Steam libraries found: {Count}",
            libraryFolders.Count);

        foreach (var library in libraryFolders)
        {
            var steamAppsPath = Path.Combine(
                library,
                "steamapps");

            if (!Directory.Exists(steamAppsPath))
            {
                _logger?.LogWarning(
                    "steamapps folder does not exist: {Path}",
                    steamAppsPath);
                continue;
            }

            _logger?.LogInformation(
                "Scanning Steam library: {Path}",
                steamAppsPath);

            string[] manifestFiles;

            try
            {
                manifestFiles = Directory.GetFiles(
                    steamAppsPath,
                    "appmanifest_*.acf",
                    SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Failed to scan Steam manifests: {Path}",
                    steamAppsPath);
                continue;
            }

            _logger?.LogInformation(
                "Found {Count} manifest files in {Path}",
                manifestFiles.Length,
                steamAppsPath);

            foreach (var manifestFile in manifestFiles)
            {
                try
                {
                    var content =
                        await File.ReadAllTextAsync(manifestFile);

                    var data =
                        VdfParser.ParseAppManifest(content);

                    data.TryGetValue("appid", out var appId);
                    data.TryGetValue("installdir", out var installDir);
                    data.TryGetValue("name", out var name);

                    if (!long.TryParse(appId, out var steamAppId))
                    {
                        _logger?.LogWarning(
                            "Invalid Steam AppID in {File}",
                            manifestFile);
                        continue;
                    }

                    var installedPath = string.IsNullOrWhiteSpace(installDir)
                        ? null
                        : Path.Combine(
                            steamAppsPath,
                            "common",
                            installDir);

                    var game = new Game
                    {
                        SteamAppId = steamAppId,
                        Name = string.IsNullOrWhiteSpace(name)
                            ? installDir ?? "Unknown Game"
                            : name,
                        InstalledPath = installedPath,
                        Status = GameStatus.Paused
                    };

                    games.Add(game);

                    _logger?.LogDebug(
                        "Imported Steam game: {Name} ({AppId})",
                        game.Name,
                        game.SteamAppId);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(
                        ex,
                        "Failed to parse Steam manifest: {File}",
                        manifestFile);
                }
            }
        }

        // Remove duplicate AppIDs
        games = games
            .GroupBy(g => g.SteamAppId)
            .Select(g => g.First())
            .ToList();

        _logger?.LogInformation(
            "Found {Count} games in Steam libraries",
            games.Count);

        return games;
    }

    private static string? ParseValue(string content, string key)
    {
        // Use a simple regex to find "key" "value" pairs robustly
        try
        {
            var pattern = $"\\\"{System.Text.RegularExpressions.Regex.Escape(key)}\\\"\\s*\\\"(?<v>.*?)\\\"";
            var m = System.Text.RegularExpressions.Regex.Match(content, pattern, System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups["v"].Value;
        }
        catch
        {
            // ignore
        }
        return null;
    }

    private string? GetSteamPathFromRegistry()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            // Try 64-bit registry view first
            using (var key64 = RegistryKey.OpenBaseKey(
                RegistryHive.CurrentUser,
                RegistryView.Registry64)
                .OpenSubKey(@"Software\Valve\Steam"))
            {
                var path64 = key64?.GetValue("SteamPath")?.ToString();

                if (!string.IsNullOrWhiteSpace(path64) && Directory.Exists(path64))
                {
                    _logger?.LogInformation("Steam found in 64-bit registry: {Path}", path64);
                    return path64.Replace("/", "\\");
                }
            }

            // Fallback to 32-bit registry view
            using (var key32 = RegistryKey.OpenBaseKey(
                RegistryHive.CurrentUser,
                RegistryView.Registry32)
                .OpenSubKey(@"Software\Valve\Steam"))
            {
                var path32 = key32?.GetValue("SteamPath")?.ToString();

                if (!string.IsNullOrWhiteSpace(path32) && Directory.Exists(path32))
                {
                    _logger?.LogInformation("Steam found in 32-bit registry: {Path}", path32);
                    return path32.Replace("/", "\\");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read Steam registry path");
        }

        return null;
    }

    private string? GetSteamPath()
    {
        // 1) Configured path in settings
        try
        {
            var cfg = _settingsService?.GetSteamPath();
            if (!string.IsNullOrEmpty(cfg) && Directory.Exists(cfg)) return cfg;
        }
        catch
        {
            // ignore
        }

        // 2) Registry
        var reg = GetSteamPathFromRegistry();
        if (!string.IsNullOrEmpty(reg) && Directory.Exists(reg)) return reg;

        return null;
    }
}

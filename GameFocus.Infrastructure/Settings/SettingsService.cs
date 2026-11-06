using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameFocus.Infrastructure.Settings;

internal class SettingsModel
{
    public string? SteamPath { get; set; }
    public int? FocusGameId { get; set; }
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsFile;
    private readonly ILogger<SettingsService>? _logger;

    public SettingsService(ILogger<SettingsService>? logger = null)
    {
        _logger = logger;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameFocus");
        Directory.CreateDirectory(dir);
        _settingsFile = Path.Combine(dir, "settings.json");
    }

    public string? GetSteamPath()
    {
        return Load().SteamPath;
    }

    public int? GetFocusGameId()
    {
        return Load().FocusGameId;
    }

    public async Task SetSteamPathAsync(string? path)
    {
        var model = Load();
        model.SteamPath = path;
        await SaveAsync(model);
        _logger?.LogInformation("Saved SteamPath to settings: {Path}", path);
    }

    public async Task SetFocusGameIdAsync(int? gameId)
    {
        var model = Load();
        model.FocusGameId = gameId;
        await SaveAsync(model);
        _logger?.LogInformation("Saved focus game id to settings: {GameId}", gameId);
    }

    // Reads the whole settings file so that one setting never overwrites another.
    private SettingsModel Load()
    {
        try
        {
            if (!File.Exists(_settingsFile)) return new SettingsModel();
            var json = File.ReadAllText(_settingsFile);
            _logger?.LogDebug("Loaded settings from {File}", _settingsFile);
            return JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
        }
        catch
        {
            _logger?.LogWarning("Failed to read settings file {File}", _settingsFile);
            return new SettingsModel();
        }
    }

    private async Task SaveAsync(SettingsModel model)
    {
        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsFile, json).ConfigureAwait(false);
    }
}

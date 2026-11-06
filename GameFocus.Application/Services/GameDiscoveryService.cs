using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;

namespace GameFocus.Application.Services;

// Use case: collect games from every registered source and store only the ones not yet known,
// and manage games the user adds from an executable.
public class GameDiscoveryService
{
    private const int MaxNameLength = 255;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IGameSource> _sources;

    public GameDiscoveryService(IUnitOfWork unitOfWork, IEnumerable<IGameSource> sources)
    {
        _unitOfWork = unitOfWork;
        _sources = sources;
    }

    // Returns how many games were added. Games already stored for the same source and external id are left untouched.
    public async Task<int> ImportFromSourcesAsync()
    {
        var added = 0;

        foreach (var source in _sources)
        {
            var discovered = await source.DiscoverAsync();
            foreach (var game in discovered)
            {
                if (string.IsNullOrWhiteSpace(game.ExternalId)) continue;

                var existing = await _unitOfWork.Games.GetByExternalIdAsync(game.Source, game.ExternalId);
                if (existing != null) continue;

                await _unitOfWork.Games.AddAsync(game);
                added++;
            }
        }

        if (added > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return added;
    }

    // Adds a game chosen from an executable. Throws ArgumentException for invalid input and
    // InvalidOperationException when another game already uses the same executable.
    public async Task<Game> AddExecutableGameAsync(string executablePath, string name, string? developer = null)
    {
        var fullPath = ValidateExecutablePath(executablePath);
        var cleanName = ValidateName(name);

        var duplicate = await FindByExecutablePathAsync(fullPath);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"Another game already uses this executable: {duplicate.Name}.");
        }

        var game = new Game
        {
            Name = cleanName,
            Developer = string.IsNullOrWhiteSpace(developer) ? null : developer.Trim(),
            Source = GameSourceType.Manual,
            ExecutablePath = fullPath,
            ExecutableName = Path.GetFileName(fullPath),
            InstalledPath = Path.GetDirectoryName(fullPath),
            Status = GameStatus.Paused,
            LastPlayed = DateTime.UtcNow
        };

        await _unitOfWork.Games.AddAsync(game);
        await _unitOfWork.SaveChangesAsync();
        return game;
    }

    // Edits the name and executable of a manual game. Games from other sources keep their own paths.
    public async Task UpdateManualGameAsync(int gameId, string name, string executablePath)
    {
        var fullPath = ValidateExecutablePath(executablePath);
        var cleanName = ValidateName(name);

        var game = (await _unitOfWork.Games.GetAllAsync()).FirstOrDefault(g => g.Id == gameId)
            ?? throw new InvalidOperationException("The game no longer exists.");

        if (game.Source != GameSourceType.Manual)
        {
            throw new InvalidOperationException("Only games added manually can have their executable path edited.");
        }

        var duplicate = await FindByExecutablePathAsync(fullPath);
        if (duplicate != null && duplicate.Id != gameId)
        {
            throw new InvalidOperationException($"Another game already uses this executable: {duplicate.Name}.");
        }

        game.Name = cleanName;
        game.ExecutablePath = fullPath;
        game.ExecutableName = Path.GetFileName(fullPath);
        game.InstalledPath = Path.GetDirectoryName(fullPath);

        _unitOfWork.Games.Update(game);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<Game?> FindByExecutablePathAsync(string fullPath)
    {
        return (await _unitOfWork.Games.GetAllAsync())
            .FirstOrDefault(g => string.Equals(g.ExecutablePath, fullPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string ValidateExecutablePath(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Choose an executable file (.exe).", nameof(executablePath));
        }

        var fullPath = Path.GetFullPath(executablePath.Trim());
        if (!string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The file must be an executable (.exe).", nameof(executablePath));
        }

        if (!File.Exists(fullPath))
        {
            throw new ArgumentException("The selected file does not exist.", nameof(executablePath));
        }

        return fullPath;
    }

    private static string ValidateName(string? name)
    {
        var clean = name?.Trim() ?? string.Empty;
        if (clean.Length == 0)
        {
            throw new ArgumentException("The game name is required.", nameof(name));
        }

        if (clean.Length > MaxNameLength)
        {
            throw new ArgumentException($"The game name can have at most {MaxNameLength} characters.", nameof(name));
        }

        return clean;
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using GameFocus.Core.Entities;

namespace GameFocus.Infrastructure.GameSources;

// Adapts the existing Steam reader to the generic game source contract.
public class SteamGameSource : IGameSource
{
    private readonly ISteamLibraryReader _steamReader;

    public SteamGameSource(ISteamLibraryReader steamReader)
    {
        _steamReader = steamReader;
    }

    public GameSourceType Source => GameSourceType.Steam;

    public async Task<IReadOnlyList<Game>> DiscoverAsync()
    {
        if (!await _steamReader.IsSteamInstalledAsync())
        {
            return new List<Game>();
        }

        var steamGames = await _steamReader.GetInstalledGamesAsync();
        return steamGames
            .Where(g => g.SteamAppId.HasValue)
            .Select(g =>
            {
                g.Source = GameSourceType.Steam;
                g.ExternalId = g.SteamAppId!.Value.ToString();
                return g;
            })
            .ToList();
    }
}

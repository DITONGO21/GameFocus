using System.Collections.Generic;
using System.Threading.Tasks;
using GameFocus.Core.Entities;

namespace GameFocus.Application.Interfaces;

public interface ISteamLibraryReader
{
    Task<bool> IsSteamInstalledAsync();
    Task<string?> GetSteamInstallPathAsync();
    Task<IEnumerable<Game>> GetInstalledGamesAsync();
}

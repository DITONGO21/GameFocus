using System.Collections.Generic;
using System.Threading.Tasks;
using GameFocus.Core.Entities;

namespace GameFocus.Core.Interfaces;

public interface IGameRepository
{
    Task<Game?> GetByIdAsync(int id);
    Task<IEnumerable<Game>> GetAllAsync();
    Task<Game?> GetBySteamIdAsync(long steamId);
    Task<Game?> GetByExternalIdAsync(GameSourceType source, string externalId);

    Task AddAsync(Game game);
    void Update(Game game);
    void Delete(Game game);
}

using System.Collections.Generic;
using System.Threading.Tasks;
using GameFocus.Core.Entities;

namespace GameFocus.Application.Interfaces;

// A place where installed games can be discovered (Steam, Epic, a manual list, ...).
// Implementations return games with Source and ExternalId set; they never write to the database.
public interface IGameSource
{
    GameSourceType Source { get; }

    Task<IReadOnlyList<Game>> DiscoverAsync();
}

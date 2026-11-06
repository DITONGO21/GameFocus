using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameFocus.Core.Entities;

namespace GameFocus.Core.Interfaces;

public interface ISessionRepository
{
    Task<GameSession?> GetByIdAsync(int id);
    Task<IEnumerable<GameSession>> GetAllAsync();
    Task<IEnumerable<GameSession>> GetByGameIdAsync(int gameId);
    Task<IEnumerable<GameSession>> GetSessionsInDateRangeAsync(DateTime start, DateTime end);
    
    Task AddAsync(GameSession session);
    void Update(GameSession session);
}

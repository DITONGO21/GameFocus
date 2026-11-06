using System.Collections.Generic;
using System.Threading.Tasks;
using GameFocus.Core.Entities;

namespace GameFocus.Core.Interfaces;

public interface ISessionInsightRepository
{
    Task<SessionIntent?> GetIntentBySessionIdAsync(int sessionId);
    Task<SessionFeedback?> GetFeedbackBySessionIdAsync(int sessionId);
    Task<IEnumerable<SessionIntent>> GetIntentsByGameIdAsync(int gameId);
    Task<IEnumerable<SessionFeedback>> GetFeedbacksByGameIdAsync(int gameId);
    Task<IEnumerable<SessionFeedback>> GetRecentFeedbacksAsync(int count);
    Task AddIntentAsync(SessionIntent intent);
    Task AddFeedbackAsync(SessionFeedback feedback);
}

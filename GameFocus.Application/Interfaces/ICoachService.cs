using System.Threading.Tasks;
using GameFocus.Core.Entities;

namespace GameFocus.Application.Interfaces;

public interface ICoachService
{
    Task AnalyzeSessionAsync(GameSession session);
}

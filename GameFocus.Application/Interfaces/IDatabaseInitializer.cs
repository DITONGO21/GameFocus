using System.Threading.Tasks;

namespace GameFocus.Application.Interfaces;

public interface IDatabaseInitializer
{
    Task EnsureDatabaseCreatedAsync();
}

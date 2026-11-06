using System.Threading.Tasks;

namespace GameFocus.Application.Interfaces;

public interface IUpdateManager
{
    Task<bool> CheckForUpdatesAsync();
    Task<bool> DownloadUpdateAsync();
    Task InstallUpdateAsync();
    string GetCurrentVersion();
}

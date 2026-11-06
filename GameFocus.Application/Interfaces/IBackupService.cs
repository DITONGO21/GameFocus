using System.Threading.Tasks;

namespace GameFocus.Application.Interfaces;

public interface IBackupService
{
    Task<bool> CreateDatabaseBackupAsync(string destinationPath);
    Task<bool> RestoreDatabaseBackupAsync(string sourcePath);
    
    Task<bool> ExportDataToJsonAsync(string destinationPath);
    Task<bool> ImportDataFromJsonAsync(string sourcePath);
    
    Task PerformAutomaticBackupAsync();
}

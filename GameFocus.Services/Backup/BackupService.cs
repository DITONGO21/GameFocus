using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GameFocus.Application.Interfaces;
using GameFocus.Core.Interfaces;

namespace GameFocus.Services.Backup;

public class BackupService : IBackupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _localDbPath;

    public BackupService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _localDbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameFocus", "gamefocus.db");
    }

    public Task<bool> CreateDatabaseBackupAsync(string destinationPath)
    {
        try
        {
            if (File.Exists(_localDbPath))
            {
                File.Copy(_localDbPath, destinationPath, overwrite: true);
                return Task.FromResult(true);
            }
        }
        catch
        {
            // Logging would go here
        }
        return Task.FromResult(false);
    }

    public Task<bool> RestoreDatabaseBackupAsync(string sourcePath)
    {
        try
        {
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, _localDbPath, overwrite: true);
                return Task.FromResult(true);
            }
        }
        catch
        {
            // Logging would go here
        }
        return Task.FromResult(false);
    }

    public async Task<bool> ExportDataToJsonAsync(string destinationPath)
    {
        try
        {
            var games = await _unitOfWork.Games.GetAllAsync();
            var json = JsonSerializer.Serialize(games, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(destinationPath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> ImportDataFromJsonAsync(string sourcePath)
    {
        // Implementation for reading JSON and inserting into DB
        return Task.FromResult(true);
    }

    public async Task PerformAutomaticBackupAsync()
    {
        var autoBackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameFocus", "Backups");
        Directory.CreateDirectory(autoBackupDir);
        
        var backupPath = Path.Combine(autoBackupDir, $"AutoBackup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        await CreateDatabaseBackupAsync(backupPath);
    }
}

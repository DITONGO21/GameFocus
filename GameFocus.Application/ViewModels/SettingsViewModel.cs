using System.Threading.Tasks;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GameFocus.Application.Interfaces;

namespace GameFocus.Application.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly ISteamLibraryReader _steamReader;

    public string? SteamPath { get; set; }

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DetectSteamCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, ISteamLibraryReader steamReader)
    {
        _settingsService = settingsService;
        _steamReader = steamReader;
        Title = "Settings";
        SteamPath = _settingsService.GetSteamPath();

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DetectSteamCommand = new AsyncRelayCommand(DetectSteamAsync);
    }

    private async Task SaveAsync()
    {
        await _settingsService.SetSteamPathAsync(SteamPath);
    }

    private async Task DetectSteamAsync()
    {
        var detected = await _steamReader.GetSteamInstallPathAsync();
        if (!string.IsNullOrEmpty(detected))
        {
            SteamPath = detected;
            await _settingsService.SetSteamPathAsync(SteamPath);
        }
    }
}

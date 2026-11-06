using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using GameFocus.Application.ViewModels;
using System;
using System.Threading.Tasks;
using WinRT.Interop;


namespace GameFocus.UI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Ensure ViewModel has current settings
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SteamPath = SteamPathBox.Text;
        await ViewModel.SaveCommand.ExecuteAsync(null);
    }

    private async void Detect_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.DetectSteamCommand.ExecuteAsync(null);
        SteamPathBox.Text = ViewModel.SteamPath;
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow!);
            InitializeWithWindow.Initialize(picker, hwnd);
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                SteamPathBox.Text = folder.Path;
                ViewModel.SteamPath = folder.Path;
                await ViewModel.SaveCommand.ExecuteAsync(null);
            }
        }
        catch (Exception)
        {
            // ignore picker errors silently
        }
    }
}

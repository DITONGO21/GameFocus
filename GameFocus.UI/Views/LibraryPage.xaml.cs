using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;
using GameFocus.Application.ViewModels;
using GameFocus.Core.Entities;

namespace GameFocus.UI.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    private int _editingGameId;

    public LibraryPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<LibraryViewModel>();
        // Pages are not cached, so each visit creates a new ViewModel; release the publisher subscription on leave.
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadGamesAsync();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = ViewModel.HasGames ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchQuery = SearchBox.Text;
    }

    private async void ImportSteam_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ImportSteamGamesCommand.ExecuteAsync(null);
        UpdateEmptyState();
    }

    private async void AddGame_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickExecutableAsync();
        if (path == null) return;

        GameNameBox.Text = System.IO.Path.GetFileNameWithoutExtension(path);
        GameDevBox.Text = string.Empty;
        GamePathBox.Text = path;
        AddErrorText.Visibility = Visibility.Collapsed;
        await AddGameDialog.ShowAsync();
    }

    private async void PauseAll_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PauseAllCommand.ExecuteAsync(null);
        UpdateEmptyState();
    }

    private async void AddGameDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var error = await SafeAsync(() => ViewModel.AddExecutableGameAsync(GamePathBox.Text, GameNameBox.Text, GameDevBox.Text));
            if (error != null)
            {
                args.Cancel = true;
                AddErrorText.Text = error;
                AddErrorText.Visibility = Visibility.Visible;
                return;
            }
            UpdateEmptyState();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void EditGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Game game)
        {
            _editingGameId = game.Id;
            EditNameBox.Text = game.Name;
            EditPathBox.Text = game.ExecutablePath ?? string.Empty;
            EditErrorText.Visibility = Visibility.Collapsed;
            await EditGameDialog.ShowAsync();
        }
    }

    private async void BrowseEditPath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickExecutableAsync();
        if (path != null)
        {
            EditPathBox.Text = path;
        }
    }

    private async void EditGameDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var error = await SafeAsync(() => ViewModel.UpdateManualGameAsync(_editingGameId, EditNameBox.Text, EditPathBox.Text));
            if (error != null)
            {
                args.Cancel = true;
                EditErrorText.Text = error;
                EditErrorText.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    // Turns unexpected failures into a message in the dialog instead of an unhandled XAML exception.
    private static async Task<string?> SafeAsync(Func<Task<string?>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            return "The operation could not be completed: " + ex.Message;
        }
    }

    // File selection is a view concern; the view model only receives the chosen path.
    private async Task<string?> PickExecutableAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };
        picker.FileTypeFilter.Add(".exe");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Current.MainWindow!));

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async void SetFocus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Game game)
        {
            await ViewModel.SetAsFocusCommand.ExecuteAsync(game);
        }
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Game game)
        {
            await ViewModel.LaunchGameCommand.ExecuteAsync(game);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Game game)
        {
            await ViewModel.DeleteGameCommand.ExecuteAsync(game);
            UpdateEmptyState();
        }
    }
}

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using GameFocus.Application.ViewModels;

namespace GameFocus.UI.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<HistoryViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            await ViewModel.LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            // Log the exception (using any logger if available) and avoid crashing
            System.Diagnostics.Debug.WriteLine($"Error loading sessions: {ex}");
        }
    }
}

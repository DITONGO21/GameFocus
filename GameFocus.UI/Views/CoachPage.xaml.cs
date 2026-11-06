using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using GameFocus.Application.ViewModels;

namespace GameFocus.UI.Views;

public sealed partial class CoachPage : Page
{
    public CoachViewModel ViewModel { get; }

    public CoachPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<CoachViewModel>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadReportsAsync();
    }

    private async void GenerateReport_Click(object sender, RoutedEventArgs e)
    {
        GenProgress.IsActive = true;
        GenProgress.Visibility = Visibility.Visible;
        GenButtonText.Text = "Generating...";

        await ViewModel.GenerateReportCommand.ExecuteAsync(null);

        GenProgress.IsActive = false;
        GenProgress.Visibility = Visibility.Collapsed;
        GenButtonText.Text = "Generate new weekly report";
    }
}

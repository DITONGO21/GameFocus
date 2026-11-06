using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using GameFocus.Application.ViewModels;

namespace GameFocus.UI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();
        DataContext = ViewModel;
        // Pages are not cached, so each visit creates a new ViewModel; release the publisher subscription on leave.
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadDataAsync();
    }
}

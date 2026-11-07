using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using GameFocus.Application.ViewModels;

namespace GameFocus.UI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    // Ticks once per second to refresh the session clock.
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    public DashboardPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();
        DataContext = ViewModel;
        _clock.Tick += (_, _) => ViewModel.UpdateElapsed(DateTime.UtcNow);
        Unloaded += (_, _) => _clock.Stop();
        // Pages are not cached, so each visit creates a new ViewModel; release the publisher subscription on leave.
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadDataAsync();
        _clock.Start();
    }
}

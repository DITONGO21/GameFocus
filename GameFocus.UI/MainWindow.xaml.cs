using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using GameFocus.UI.Views;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace GameFocus.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(DashboardPage));
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            ContentFrame.Navigate(typeof(Views.SettingsPage));
            return;
        }

        if (args.InvokedItemContainer?.Tag is string tag)
        {
            Type? pageType = tag switch
            {
                "Dashboard" => typeof(DashboardPage),
                "Library" => typeof(LibraryPage),
                "History" => typeof(HistoryPage),
                "Goals" => typeof(GoalsPage),
                "Coach" => typeof(CoachPage),
                _ => null
            };

            if (pageType != null)
            {
                ContentFrame.Navigate(pageType);
            }
            else
            {
                var logger = App.Current.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MainWindow>>();
                logger?.LogWarning("Unknown navigation tag: {Tag}", tag);
            }
        }
    }
}

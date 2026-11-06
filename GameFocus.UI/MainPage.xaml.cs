using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using GameFocus.Application.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GameFocus.UI;

/// <summary>
/// The main content page displayed inside the application window.
/// Add your UI logic, event handlers, and data binding here.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();

        // Initialize view model for the main page
        var vm = App.Current.Services.GetService<DashboardViewModel>();
        if (vm != null)
        {
            DataContext = vm;
        }
    }
}

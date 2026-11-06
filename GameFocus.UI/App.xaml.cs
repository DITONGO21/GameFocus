using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using GameFocus.Core.Interfaces;
using GameFocus.Application.Interfaces;
using GameFocus.Application.Services;
using GameFocus.Application.ViewModels;
using GameFocus.Infrastructure.Database;
using GameFocus.Infrastructure.Repositories;
using GameFocus.Infrastructure.Services;
using GameFocus.Infrastructure.Steam;
using GameFocus.Infrastructure.Settings;
using Serilog;
using Microsoft.Extensions.Logging;
using GameFocus.Services.Monitoring;
using GameFocus.Services.Backup;
using GameFocus.Services.Updates;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace GameFocus.UI;

public partial class App : Microsoft.UI.Xaml.Application
{
    public new static App Current => (App)Microsoft.UI.Xaml.Application.Current;
    public IServiceProvider Services { get; }
    public Window? MainWindow { get; private set; }

    public App()
    {
        this.InitializeComponent();

        // Configure Serilog for file-based logging early
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameFocus", "logs", "gamefocus-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Services = ConfigureServices();

        // Setup global exception handlers
        this.UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled XAML exception occurred.");
        e.Handled = true; // Prevent immediate crash if possible, though some crashes are fatal.
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "AppDomain unhandled exception occurred. IsTerminating: {IsTerminating}", e.IsTerminating);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unobserved task exception occurred.");
        e.SetObserved();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var logger = Services.GetService<Microsoft.Extensions.Logging.ILogger<App>>();
        // Initialize Database
        var dbInitializer = Services.GetService<IDatabaseInitializer>();
        if (dbInitializer != null)
        {
            try
            {
                await dbInitializer.EnsureDatabaseCreatedAsync();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Database initialization failed");
            }
        }

        // Initialize SessionCoordinator
        var coordinator = Services.GetService<GameFocus.Application.Services.SessionCoordinator>();
        if (coordinator != null)
        {
            try
            {
                await coordinator.InitializeAsync();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "SessionCoordinator initialization failed");
            }
        }

        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Ensure DB directory exists
        var dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameFocus");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "gamefocus.db");

        // Database
        services.AddDbContext<GameFocusDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Repositories
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IHabitReportRepository, HabitReportRepository>();
        services.AddScoped<ISessionInsightRepository, SessionInsightRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Infrastructure Services
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IGameProcessMonitor, WindowsProcessMonitor>();
        // Settings
        services.AddSingleton<ISettingsService, SettingsService>();

        // UI Dispatcher & Overlay
        services.AddSingleton<IUIDispatcher>(new GameFocus.UI.Services.UIDispatcher(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
        services.AddSingleton<IUIOverlayService>(new GameFocus.UI.Services.UIOverlayService(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));

        // Logging
        services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

        // Services
        services.AddSingleton<ISteamLibraryReader, SteamLibraryReader>();
        services.AddSingleton<IGameSource, GameFocus.Infrastructure.GameSources.SteamGameSource>();
        services.AddSingleton<GameFocus.Infrastructure.GameSources.IWindowsRegistry, GameFocus.Infrastructure.GameSources.WindowsRegistry>();
        services.AddSingleton<IGameSource, GameFocus.Infrastructure.GameSources.EpicGameSource>();
        services.AddSingleton<IGameSource, GameFocus.Infrastructure.GameSources.RiotGameSource>();
        services.AddSingleton<IGameSource, GameFocus.Infrastructure.GameSources.XboxGameSource>();
        services.AddSingleton<IGameSource, GameFocus.Infrastructure.GameSources.GogGameSource>();
        services.AddSingleton<IGameSource, GameFocus.Infrastructure.GameSources.UbisoftGameSource>();
        services.AddSingleton<IGameSource, GameFocus.Infrastructure.GameSources.EaGameSource>();
        services.AddSingleton<IGameSource, GameFocus.Infrastructure.GameSources.RockstarGameSource>();
        services.AddSingleton<IGameSource, GameFocus.Infrastructure.GameSources.HytaleGameSource>();
        services.AddTransient<GameDiscoveryService>();
        services.AddSingleton<IGameSessionService, GameSessionService>();
        // Application services
        services.AddSingleton<IDashboardPublisher, DashboardPublisher>();
        services.AddSingleton<SessionCoordinator>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IUpdateManager, UpdateManager>();
        
        services.AddTransient<GameFocusCoachService>();
        services.AddTransient<ICoachService>(sp => sp.GetRequiredService<GameFocusCoachService>());
        services.AddTransient<IGoalService, GoalService>();
        services.AddTransient<IGamingPatternAnalyzer, GamingPatternAnalyzer>();

        // ViewModels
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<GoalsViewModel>();
        services.AddTransient<CoachViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<HistoryViewModel>();

        return services.BuildServiceProvider();
    }
}

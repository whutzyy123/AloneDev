using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using PMTool.Application.Abstractions;
using PMTool.Application.DependencyInjection;
using PMTool.Core.Abstractions;
using PMTool.Infrastructure.DependencyInjection;
using PMTool.App.Services;
using PMTool.App.ViewModels;
using PMTool.App.Views.Shell;

namespace PMTool.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private static readonly string StartupLogPath = GetStartupLogPath();

    /// <summary>Composition root; set at startup.</summary>
    public static ServiceProvider Services { get; private set; } = null!;

    public App()
    {
        UnhandledException += OnUnhandledException;
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        Services = ConfigureServices();
        _ = InitializeAppDataAsync();

        var window = _window ??= Window.Current ?? new Window();
        window.Title = "AloneDev";

        if (window.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            window.Content = rootFrame;
        }

        _ = rootFrame.Navigate(typeof(MainShellPage), e.Arguments);
        window.Activate();
    }

    private async Task InitializeAppDataAsync()
    {
        try
        {
            var init = Services.GetRequiredService<IAppInitializationService>();
            await init.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            TryLogStartupException(ex);
        }
    }

    private static void TryLogStartupException(Exception ex)
    {
        try
        {
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z OnLaunched init: {ex}{Environment.NewLine}";
            File.AppendAllText(StartupLogPath, line);
        }
        catch
        {
            // ignore
        }
    }

    private static string GetStartupLogPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AloneDev");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "startup.log");
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddPmToolInfrastructure();
        services.AddPmToolApplication();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<GlobalSearchUiCoordinator>();
        services.AddTransient<ModulePlaceholderViewModel>();
        services.AddTransient<ProjectListViewModel>();
        return services.BuildServiceProvider();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try
        {
            if (Services.GetService(typeof(IErrorLogger)) is IErrorLogger log)
            {
                log.LogException(e.Exception, "UnhandledException");
            }
        }
        catch
        {
            // Intentionally empty: logger must not rethrow.
        }
    }

    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load Page {e.SourcePageType.FullName}");
    }
}

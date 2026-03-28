using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Navigation;
using PMTool.Application.Abstractions;
using PMTool.Application.DependencyInjection;
using PMTool.Core.Abstractions;
using PMTool.Core.Models.Settings;
using PMTool.Infrastructure.DependencyInjection;
using PMTool.App.Services;
using PMTool.App.ViewModels;
using PMTool.App.Views.Shell;
using Windows.UI.ViewManagement;

namespace PMTool.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;

    /// <summary>主窗口（供文件选择器等 Win32 互操作使用）。</summary>
    public static Window? MainWindow { get; private set; }

    private static readonly string StartupLogPath = GetStartupLogPath();

    /// <summary>Composition root; set at startup.</summary>
    public static ServiceProvider Services { get; private set; } = null!;

    private static UISettings? _systemUiSettings;
    private static DispatcherQueueTimer? _systemThemeDebounceTimer;

    public App()
    {
        UnhandledException += OnUnhandledException;
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        Services = ConfigureServices();

        var window = _window ??= Window.Current ?? new Window();
        MainWindow = window;
        window.Title = "AloneDev";

        if (window.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            window.Content = rootFrame;
        }

        // WinUI：若在首次 await 之后才 Activate，壳层可能长时间不呈现窗口（CLI/dotnet watch 下尤易表现为「已启动但无窗口」）。
        window.Activate();

        _ = LaunchSequenceAsync(window, rootFrame, e);
    }

    private async Task LaunchSequenceAsync(Window window, Frame rootFrame, LaunchActivatedEventArgs e)
    {
        try
        {
            try
            {
                await InitializeAppDataAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                TryLogStartupException(ex);
            }

            if (!rootFrame.Navigate(typeof(MainShellPage), e.Arguments))
            {
                throw new InvalidOperationException("Failed to load MainShellPage.");
            }

            window.Activate();

            try
            {
                // 将主题应用延后到窗口激活后，规避启动早期 COM 状态不稳定导致的 RequestedTheme 失败。
                await ApplyRequestedThemeFromConfigAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                TryLogStartupException(ex);
            }

            try
            {
                var scheduler = Services.GetRequiredService<AutoBackupScheduler>();
                scheduler.Start();
                await scheduler.TryStartupCatchUpAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                TryLogStartupException(ex);
            }
        }
        catch (Exception ex)
        {
            TryLogStartupException(ex);
            window.Activate();
        }
    }

    private static async Task ApplyRequestedThemeFromConfigAsync()
    {
        var store = Services.GetRequiredService<IAppConfigStore>();
        var cfg = await store.LoadAsync().ConfigureAwait(true);
        ApplyRequestedTheme(cfg.Theme);
    }

    /// <summary>供设置页实时切换主题复用。</summary>
    public static void ApplyRequestedTheme(AppThemeOption theme)
    {
        switch (theme)
        {
            case AppThemeOption.Light:
                TryApplyTheme(ApplicationTheme.Light);
                break;
            case AppThemeOption.Dark:
                TryApplyTheme(ApplicationTheme.Dark);
                break;
            default:
                ApplyFollowSystemThemeFromUiSettings();
                break;
        }

        UpdateSystemThemeListener(theme);
    }

    /// <summary>按当前系统背景亮度设置 <see cref="ApplicationTheme"/>（跟随系统 / 系统色彩变化回调复用）。</summary>
    private static void ApplyFollowSystemThemeFromUiSettings()
    {
        try
        {
            _systemUiSettings ??= new UISettings();
            var c = _systemUiSettings.GetColorValue(UIColorType.Background);
            var lum = (c.R + c.G + c.B) / 3.0;
            TryApplyTheme(lum < 128 ? ApplicationTheme.Dark : ApplicationTheme.Light);
        }
        catch
        {
            TryApplyTheme(ApplicationTheme.Light);
        }
    }

    private static void TryApplyTheme(ApplicationTheme targetTheme)
    {
        try
        {
            var app = Microsoft.UI.Xaml.Application.Current;
            app.RequestedTheme = targetTheme;
            return;
        }
        catch
        {
            // fallback to root element below
        }

        try
        {
            if (MainWindow?.Content is FrameworkElement root)
            {
                root.RequestedTheme = targetTheme switch
                {
                    ApplicationTheme.Dark => ElementTheme.Dark,
                    _ => ElementTheme.Light,
                };
            }
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// <see cref="AppThemeOption.FollowSystem"/> 时订阅系统深浅色变化；固定浅/深时退订并停止防抖定时器。
    /// </summary>
    private static void UpdateSystemThemeListener(AppThemeOption theme)
    {
        _systemUiSettings ??= new UISettings();
        _systemUiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
        _systemThemeDebounceTimer?.Stop();
        _systemThemeDebounceTimer = null;
        if (theme != AppThemeOption.FollowSystem)
        {
            return;
        }

        _systemUiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
    }

    private static void OnSystemColorValuesChanged(UISettings sender, object args)
    {
        var dq = MainWindow?.DispatcherQueue;
        if (dq is null)
        {
            return;
        }

        if (dq.HasThreadAccess)
        {
            ScheduleFollowSystemReapply(dq);
        }
        else
        {
            dq.TryEnqueue(() => ScheduleFollowSystemReapply(dq));
        }

        static void ScheduleFollowSystemReapply(DispatcherQueue q)
        {
            _systemThemeDebounceTimer ??= q.CreateTimer();
            _systemThemeDebounceTimer.Interval = TimeSpan.FromMilliseconds(200);
            _systemThemeDebounceTimer.IsRepeating = false;
            _systemThemeDebounceTimer.Tick -= OnSystemThemeDebounceTick;
            _systemThemeDebounceTimer.Tick += OnSystemThemeDebounceTick;
            _systemThemeDebounceTimer.Stop();
            _systemThemeDebounceTimer.Start();
        }
    }

    private static async void OnSystemThemeDebounceTick(DispatcherQueueTimer timer, object o)
    {
        timer.Tick -= OnSystemThemeDebounceTick;
        try
        {
            var store = Services.GetRequiredService<IAppConfigStore>();
            var cfg = await store.LoadAsync().ConfigureAwait(true);
            if (cfg.Theme != AppThemeOption.FollowSystem)
            {
                return;
            }

            ApplyFollowSystemThemeFromUiSettings();
        }
        catch
        {
            // ignore
        }
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
        services.AddSingleton<ProjectListViewModel>();
        services.AddSingleton<DisabledOperationBarViewModel>();
        services.AddSingleton<FeatureListViewModel>();
        services.AddSingleton<TaskListViewModel>();
        services.AddSingleton<ReleaseListViewModel>();
        services.AddSingleton<DocumentListViewModel>();
        services.AddSingleton<IdeaListViewModel>();
        services.AddSingleton<GlobalSearchViewModel>();
        services.AddSingleton<GlobalSearchUiCoordinator>();
        services.AddSingleton<IGlobalSearchFlyout>(sp => sp.GetRequiredService<GlobalSearchUiCoordinator>());
        services.AddSingleton<ShellViewModel>(sp => new ShellViewModel(
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<ProjectListViewModel>(),
            sp.GetRequiredService<FeatureListViewModel>(),
            sp.GetRequiredService<TaskListViewModel>(),
            sp.GetRequiredService<ReleaseListViewModel>(),
            sp.GetRequiredService<DocumentListViewModel>(),
            sp.GetRequiredService<IdeaListViewModel>(),
            sp.GetRequiredService<DataManagementViewModel>(),
            sp.GetRequiredService<DisabledOperationBarViewModel>(),
            () => sp.GetRequiredService<SettingsViewModel>()));
        services.AddSingleton<IShellNavCoordinator>(sp => new ShellNavCoordinator(sp.GetRequiredService<ShellViewModel>()));
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainShellShortcutController>();
        services.AddSingleton<IGlobalSearchNavigationService, GlobalSearchNavigator>();
        services.AddSingleton<AccountManagementViewModel>();
        services.AddSingleton<DataManagementViewModel>();
        services.AddSingleton<AutoBackupScheduler>();
        services.AddTransient<ModulePlaceholderViewModel>();
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

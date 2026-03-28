using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PMTool.Application.Abstractions;
using PMTool.App.Services;
using PMTool.Core.Abstractions;
using PMTool.Core.Models.Settings;

namespace PMTool.App.ViewModels;

public partial class SettingsViewModel(
    IAppConfigStore appConfigStore,
    IDataRootProvider dataRootProvider,
    IDataRootMigrationService migrationService,
    IAppInitializationService appInitializationService,
    IShellNavCoordinator shellNavCoordinator) : ObservableObject
{
    private bool _suppressThemePersist;

    [ObservableProperty]
    private AppThemeOption _selectedTheme = AppThemeOption.FollowSystem;

    [RelayCommand]
    private void SelectTheme(AppThemeOption? option)
    {
        if (option is { } o)
        {
            SelectedTheme = o;
        }
    }

    [ObservableProperty]
    private string _dataRootPathDisplay = "";

    [ObservableProperty]
    private string _configDataPathNote = "";

    [ObservableProperty]
    private string _migrationTargetPath = "";

    [ObservableProperty]
    private double _migrationPercent;

    [ObservableProperty]
    private string _migrationStatusText = "";

    [ObservableProperty]
    private bool _isMigrating;

    [ObservableProperty]
    private string _errorBanner = "";

    [ObservableProperty]
    private bool _successVisible;

    [ObservableProperty]
    private string _successMessage = "";

    public ObservableCollection<SettingsShortcutRowViewModel> ShortcutRows { get; } = [];

    partial void OnSelectedThemeChanged(AppThemeOption value)
    {
        if (_suppressThemePersist)
        {
            return;
        }

        _ = PersistThemeAsync(value);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ErrorBanner = "";
        var cfg = await appConfigStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        _suppressThemePersist = true;
        SelectedTheme = cfg.Theme;
        _suppressThemePersist = false;
        DataRootPathDisplay = dataRootProvider.GetDataRootPath();
        ConfigDataPathNote = string.IsNullOrWhiteSpace(cfg.DataPath)
            ? "未单独记录（与当前生效根一致或使用默认）"
            : cfg.DataPath;
        AppShortcutDefaults.WithDefaultShortcuts(cfg);
        ShortcutRows.Clear();
        foreach (ShortcutActionId id in Enum.GetValues<ShortcutActionId>())
        {
            var name = id.ToString();
            _ = cfg.Shortcuts.TryGetValue(name, out var bind);
            ShortcutRows.Add(new SettingsShortcutRowViewModel(id, ShortcutLabel(id), bind ?? ""));
        }
    }

    [RelayCommand]
    private async Task SaveShortcutsAsync(CancellationToken cancellationToken = default)
    {
        ErrorBanner = "";
        var cfg = await appConfigStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        foreach (var row in ShortcutRows)
        {
            cfg.Shortcuts[row.ActionId.ToString()] = row.BindingDisplay.Trim();
        }

        AppShortcutDefaults.WithDefaultShortcuts(cfg);
        cfg.Shortcuts[nameof(ShortcutActionId.GlobalSearch)] = AppShortcutDefaults.GlobalSearch;
        if (!ShortcutBindingParser.TryValidateShortcutTable(cfg.Shortcuts, out var err))
        {
            ErrorBanner = err ?? "快捷键无效。";
            return;
        }

        foreach (var row in ShortcutRows)
        {
            if (cfg.Shortcuts.TryGetValue(row.ActionId.ToString(), out var v) &&
                ShortcutBindingParser.TryNormalizeDisplay(v, out var norm, out _))
            {
                row.BindingDisplay = norm;
            }
        }

        await appConfigStore.SaveAsync(cfg, cancellationToken).ConfigureAwait(true);
        MainShellShortcutReload.RequestReload?.Invoke();
        await FlashSuccessAsync("设置已保存。").ConfigureAwait(true);
    }

    [RelayCommand]
    private void ResetShortcutsToDefaults()
    {
        var tmp = AppShortcutDefaults.WithDefaultShortcuts(new AppConfiguration());
        foreach (var row in ShortcutRows)
        {
            if (row.IsReadOnlyBinding)
            {
                row.BindingDisplay = AppShortcutDefaults.GlobalSearch;
                continue;
            }

            if (tmp.Shortcuts.TryGetValue(row.ActionId.ToString(), out var v))
            {
                row.BindingDisplay = v;
            }
        }
    }

    [RelayCommand]
    private void OpenDataManagement() => shellNavCoordinator.SelectFooterNav("data");

    [RelayCommand]
    private async Task RunMigrationAsync(CancellationToken cancellationToken = default)
    {
        ErrorBanner = "";
        if (string.IsNullOrWhiteSpace(MigrationTargetPath))
        {
            ErrorBanner = "请先填写或选择新的空文件夹路径。";
            return;
        }

        IsMigrating = true;
        MigrationPercent = 0;
        MigrationStatusText = "准备迁移…";
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var progress = new Progress<(string message, int percent)>(p =>
            {
                MigrationStatusText = p.message;
                MigrationPercent = p.percent;
            });
            await migrationService
                .RunAsync(MigrationTargetPath.Trim(), progress, linked.Token)
                .ConfigureAwait(true);
            await appInitializationService.InitializeAsync(linked.Token).ConfigureAwait(true);
            await RefreshAsync(linked.Token).ConfigureAwait(true);
            MainShellShortcutReload.RequestReload?.Invoke();
            await FlashSuccessAsync("路径迁移完成。").ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            ErrorBanner = "迁移已取消。";
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
        finally
        {
            IsMigrating = false;
            MigrationStatusText = "";
            MigrationPercent = 0;
        }
    }

    private async Task PersistThemeAsync(AppThemeOption theme)
    {
        try
        {
            var cfg = await appConfigStore.LoadAsync().ConfigureAwait(true);
            cfg.Theme = theme;
            await appConfigStore.SaveAsync(cfg).ConfigureAwait(true);
            App.ApplyRequestedTheme(theme);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private async Task FlashSuccessAsync(string message)
    {
        SuccessMessage = message;
        SuccessVisible = true;
        await Task.Delay(2000).ConfigureAwait(true);
        SuccessVisible = false;
    }

    private static string ShortcutLabel(ShortcutActionId id) =>
        id switch
        {
            ShortcutActionId.NewProject => "新建项目",
            ShortcutActionId.NewFeature => "新建特性",
            ShortcutActionId.NewTask => "新建任务",
            ShortcutActionId.NewDocument => "新建文档",
            ShortcutActionId.NewIdea => "新建灵感",
            ShortcutActionId.GlobalSearch => "全局搜索（固定 Ctrl+K）",
            ShortcutActionId.Save => "保存当前文档",
            ShortcutActionId.Undo => "撤销",
            ShortcutActionId.Redo => "重做",
            _ => id.ToString(),
        };
}

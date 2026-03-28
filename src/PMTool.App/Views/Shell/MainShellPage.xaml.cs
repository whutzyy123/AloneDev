using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using PMTool.Application.Abstractions;
using PMTool.App.Diagnostics;
using PMTool.App.Services;
using PMTool.App.UI;
using PMTool.App.ViewModels;
using PMTool.Core.Abstractions;
using Windows.Storage;

namespace PMTool.App.Views.Shell;

public sealed partial class MainShellPage : Page
{
    private const string GlobalSearchShortcutTipSettingsKey = "AloneDev.Ui.GlobalSearchShortcutTipShown";

    private readonly KeyEventHandler _shellGlobalSearchKeyDownHandler;

    public ShellViewModel ViewModel { get; }
    public AccountManagementViewModel AccountVm { get; }
    public GlobalSearchViewModel GlobalSearchVm { get; }

    public MainShellPage()
    {
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        AccountVm = App.Services.GetRequiredService<AccountManagementViewModel>();
        GlobalSearchVm = App.Services.GetRequiredService<GlobalSearchViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        var nav = App.Services.GetRequiredService<INavigationService>();
        nav.ContentFrame = ContentFrame;
        ContentFrame.NavigationFailed += ContentFrame_NavigationFailed;
        _shellGlobalSearchKeyDownHandler = ShellGlobalSearchBox_SearchKeyDown;
        ShellGlobalSearchBox.AddHandler(UIElement.KeyDownEvent, _shellGlobalSearchKeyDownHandler, handledEventsToo: true);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ShellGlobalSearchBox.RemoveHandler(UIElement.KeyDownEvent, _shellGlobalSearchKeyDownHandler);
        ContentFrame.NavigationFailed -= ContentFrame_NavigationFailed;
        MainShellShortcutReload.RequestReload = null;
    }

    private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        // #region agent log
        DebugAgentLog.Write(
            "N",
            "MainShellPage.ContentFrame.NavigationFailed",
            "inner frame",
            new Dictionary<string, string>
            {
                ["page"] = e.SourcePageType?.Name ?? "",
                ["msg"] = e.Exception?.Message ?? "",
            });
        // #endregion
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var accountManagement = App.Services.GetRequiredService<IAccountManagementService>();
        accountManagement.CurrentAccountChanged += (_, _) =>
        {
            _ = DispatcherQueue.TryEnqueue(() => ViewModel.ActivateDefaultNav());
        };

        await TryResumeDataRootMigrationIfNeededAsync().ConfigureAwait(true);

        WindowChromeHelper.ApplyMicaAndTitleBar(App.MainWindow, TitleBarDragRegion);

        var shortcutController = App.Services.GetRequiredService<MainShellShortcutController>();
        await shortcutController.ReloadAsync(this).ConfigureAwait(true);
        MainShellShortcutReload.RequestReload = () => { _ = shortcutController.ReloadAsync(this); };

        ViewModel.ActivateDefaultNav();
        TryScheduleGlobalSearchShortcutTip();
    }

    private void TryScheduleGlobalSearchShortcutTip()
    {
        if (ApplicationData.Current.LocalSettings.Values.TryGetValue(GlobalSearchShortcutTipSettingsKey, out var v)
            && v is true)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            GlobalSearchShortcutTip.Target = ShellGlobalSearchBox;
            GlobalSearchShortcutTip.IsOpen = true;
        });
    }

    private void GlobalSearchShortcutTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
    {
        ApplicationData.Current.LocalSettings.Values[GlobalSearchShortcutTipSettingsKey] = true;
    }

    private void GlobalSearchShortcutTip_ActionButtonClick(TeachingTip sender, object e)
    {
        ApplicationData.Current.LocalSettings.Values[GlobalSearchShortcutTipSettingsKey] = true;
        GlobalSearchShortcutTip.IsOpen = false;
    }

    private async Task TryResumeDataRootMigrationIfNeededAsync()
    {
        var migration = App.Services.GetRequiredService<IDataRootMigrationService>();
        var pending = await migration.GetPendingStateAsync().ConfigureAwait(true);
        if (pending is null)
        {
            return;
        }

        var dialog = AloneDialogFactory.CreateStandardWithSecondary(
            XamlRoot,
            "路径迁移未完成",
            "上次将数据迁移到新目录时未完成，是否从断点继续？选择「放弃」将清除迁移状态并保持当前数据根不变。",
            "继续迁移",
            "放弃迁移",
            ContentDialogButton.Primary);

        var result = await dialog.ShowAsync().AsTask().ConfigureAwait(true);
        if (result == ContentDialogResult.Secondary)
        {
            await migration.RollbackPendingOnlyAsync().ConfigureAwait(true);
            return;
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await migration
                .RunAsync(
                    null,
                    new Progress<(string message, int percent)>(_ => { }),
                    CancellationToken.None)
                .ConfigureAwait(true);
            var init = App.Services.GetRequiredService<IAppInitializationService>();
            await init.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            var err = AloneDialogFactory.CreateInfo(XamlRoot, "迁移失败", ex.Message);
            _ = await err.ShowAsync().AsTask().ConfigureAwait(true);
        }
    }

    private async void AccountFlyout_Opening(object sender, object e)
    {
        await AccountVm.RefreshAsync().ConfigureAwait(true);
    }

    private async void DeleteAccountButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (AccountVm.SelectedAccountName is not { Length: > 0 } name)
        {
            return;
        }

        var confirm = AloneDialogFactory.CreateDestructiveConfirm(
            XamlRoot,
            "删除本地账号",
            $"将移除账号「{name}」及其数据目录（含 pmtool.db），且不可恢复。是否继续？",
            "删除");

        if (await confirm.ShowAsync().AsTask().ConfigureAwait(true) != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await App.Services.GetRequiredService<IAccountManagementService>().DeleteAccountAsync(name).ConfigureAwait(true);
            await AccountVm.RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            var err = AloneDialogFactory.CreateInfo(XamlRoot, "无法删除", ex.Message);
            _ = await err.ShowAsync().AsTask().ConfigureAwait(true);
        }
    }

    private void ShellGlobalSearchBox_GotFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        EnqueueOpenGlobalSearch();
    }

    private void GlobalSearchAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        EnqueueOpenGlobalSearch();
    }

    /// <summary>
    /// 不在 KeyboardAccelerator / 同步输入回调里直接 ShowAt Flyout，否则 WinUI 上易与输入队列死锁表现为卡死。
    /// </summary>
    private void EnqueueOpenGlobalSearch()
    {
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, OpenGlobalSearchCore);
    }

    private void OpenGlobalSearchCore()
    {
        try
        {
            if (GlobalSearchShortcutTip is { } tip)
            {
                tip.IsOpen = false;
            }
        }
        catch
        {
            // TeachingTip 未就绪时忽略
        }

        var flyoutSvc = App.Services.GetRequiredService<IGlobalSearchFlyout>();
        FrameworkElement? anchor = ShellGlobalSearchBox;
        anchor ??= MainOperationBar;
        if (anchor is null)
        {
            return;
        }

        flyoutSvc.TryOpen(anchor);
    }

    private void ShellGlobalSearchBox_SearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var flyoutSvc = App.Services.GetRequiredService<IGlobalSearchFlyout>();
        if (!flyoutSvc.IsOpen)
        {
            return;
        }

        var vm = GlobalSearchVm;
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Escape:
                e.Handled = true;
                flyoutSvc.Close();
                return;
            case Windows.System.VirtualKey.Enter:
                e.Handled = true;
                _ = vm.TryHandleEnterForSelectionAsync();
                return;
        }

        if (vm.GetDisplayedHitCount() == 0)
        {
            return;
        }

        var focusedEl = FocusManager.GetFocusedElement(XamlRoot);
        var focusedDo = focusedEl as DependencyObject;
        var inQuery = IsDescendantOfSearchHost(focusedDo, ShellGlobalSearchBox);

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Down:
                e.Handled = true;
                if (inQuery)
                {
                    vm.FocusFirstHit();
                }
                else
                {
                    vm.MoveFocusedHit(1);
                }

                flyoutSvc.SyncHitFocusFromViewModel();
                return;
            case Windows.System.VirtualKey.Up:
                if (inQuery)
                {
                    return;
                }

                e.Handled = true;
                vm.MoveFocusedHit(-1);
                if (vm.FocusedHitFlatIndex < 0)
                {
                    _ = ShellGlobalSearchBox.Focus(FocusState.Programmatic);
                }
                else
                {
                    flyoutSvc.SyncHitFocusFromViewModel();
                }

                return;
            case Windows.System.VirtualKey.Home:
                if (inQuery)
                {
                    return;
                }

                e.Handled = true;
                vm.FocusFirstHit();
                flyoutSvc.SyncHitFocusFromViewModel();
                return;
            case Windows.System.VirtualKey.End:
                if (inQuery)
                {
                    return;
                }

                e.Handled = true;
                vm.FocusLastHit();
                flyoutSvc.SyncHitFocusFromViewModel();
                return;
        }
    }

    private static bool IsDescendantOfSearchHost(DependencyObject? node, DependencyObject? searchRoot)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, searchRoot))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }
}

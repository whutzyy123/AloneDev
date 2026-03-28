using System.Runtime.InteropServices.WindowsRuntime;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using PMTool.Application.Abstractions;
using PMTool.App.Services;
using PMTool.App.UI;
using PMTool.App.ViewModels;
using PMTool.Core.Abstractions;
using Windows.Storage;

namespace PMTool.App.Views.Shell;

public sealed partial class MainShellPage : Page
{
    private const string GlobalSearchShortcutTipSettingsKey = "AloneDev.Ui.GlobalSearchShortcutTipShown";
    private const double ExpandedSearchBoxWidth = 680d;
    private const double CollapsedSearchBoxScale = 0.92d;
    private static bool _globalSearchShortcutTipShownThisSession;

    private readonly KeyEventHandler _shellGlobalSearchKeyDownHandler;
    private bool _isGlobalSearchExpanded;
    private bool _isSearchAnimating;
    private bool _isGlobalSearchShortcutTipScheduled;

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
        ViewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
        ShellGlobalSearchBox.RemoveHandler(UIElement.KeyDownEvent, _shellGlobalSearchKeyDownHandler);
        ContentFrame.NavigationFailed -= ContentFrame_NavigationFailed;
        MainShellShortcutReload.RequestReload = null;
    }

    private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
    {
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
        ViewModel.PropertyChanged += OnShellViewModelPropertyChanged;
        SyncGlobalSearchScopeToCurrentModule();
        SetGlobalSearchExpanded(false);
        TryScheduleGlobalSearchShortcutTip();
    }

    private void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.ActiveNavKey)
            || e.PropertyName == nameof(ShellViewModel.ModuleTitle))
        {
            SyncGlobalSearchScopeToCurrentModule();
        }
    }

    private void SyncGlobalSearchScopeToCurrentModule()
    {
        GlobalSearchVm.ApplyCurrentModuleScope(ViewModel.ActiveNavKey, ViewModel.ModuleTitle);
    }

    private void TryScheduleGlobalSearchShortcutTip()
    {
        if (_globalSearchShortcutTipShownThisSession || _isGlobalSearchShortcutTipScheduled || IsGlobalSearchShortcutTipSeen())
        {
            return;
        }

        _isGlobalSearchShortcutTipScheduled = true;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (IsGlobalSearchShortcutTipSeen())
            {
                return;
            }

            GlobalSearchShortcutTip.Target = SearchExpandButton;
            GlobalSearchShortcutTip.IsOpen = true;
            _globalSearchShortcutTipShownThisSession = true;
        });
    }

    private void GlobalSearchShortcutTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
    {
        MarkGlobalSearchShortcutTipSeen();
    }

    private void GlobalSearchShortcutTip_ActionButtonClick(TeachingTip sender, object e)
    {
        MarkGlobalSearchShortcutTipSeen();
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
            "删除工作空间",
            $"将移除工作空间「{name}」及其数据目录（含 pmtool.db），且不可恢复。是否继续？",
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
        ExpandGlobalSearch();
        EnqueueOpenGlobalSearch();
    }

    private void SearchExpandButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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
        ExpandGlobalSearch();
        try
        {
            if (GlobalSearchShortcutTip is { } tip)
            {
                tip.IsOpen = false;
                MarkGlobalSearchShortcutTipSeen();
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
        _ = ShellGlobalSearchBox.Focus(FocusState.Programmatic);
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
                if (string.IsNullOrWhiteSpace(ShellGlobalSearchBox.Text))
                {
                    CollapseGlobalSearch();
                }

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

    private void SetGlobalSearchExpanded(bool expanded)
    {
        if (expanded)
        {
            ExpandGlobalSearch();
            return;
        }

        _isSearchAnimating = false;
        _isGlobalSearchExpanded = false;
        ShellGlobalSearchBox.Visibility = Visibility.Collapsed;
        ShellGlobalSearchBox.Width = ExpandedSearchBoxWidth;
        ShellGlobalSearchBox.Opacity = 0;
        if (ShellGlobalSearchBox.RenderTransform is ScaleTransform st)
        {
            st.ScaleX = CollapsedSearchBoxScale;
            st.ScaleY = 1;
        }

        ShellGlobalSearchBox.Text = "";
    }

    private void RootLayout_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (GlobalSearchShortcutTip.IsOpen)
        {
            GlobalSearchShortcutTip.IsOpen = false;
            MarkGlobalSearchShortcutTipSeen();
        }

        if (!_isGlobalSearchExpanded || _isSearchAnimating)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(ShellGlobalSearchBox.Text))
        {
            return;
        }

        var src = e.OriginalSource as DependencyObject;
        if (IsDescendantOfSearchHost(src, ShellGlobalSearchBox) || IsDescendantOfSearchHost(src, SearchExpandButton))
        {
            return;
        }

        CollapseGlobalSearch();
    }

    private void ExpandGlobalSearch()
    {
        if (_isSearchAnimating || _isGlobalSearchExpanded)
        {
            return;
        }

        _isSearchAnimating = true;
        ShellGlobalSearchBox.Visibility = Visibility.Visible;
        ShellGlobalSearchBox.Width = ExpandedSearchBoxWidth;
        ShellGlobalSearchBox.Opacity = 0;
        if (ShellGlobalSearchBox.RenderTransform is ScaleTransform st)
        {
            st.ScaleX = CollapsedSearchBoxScale;
            st.ScaleY = 1;
        }

        var sb = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(200);

        var scaleXAnim = new DoubleAnimation
        {
            From = CollapsedSearchBoxScale,
            To = 1,
            Duration = duration,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(scaleXAnim, ShellGlobalSearchBox);
        Storyboard.SetTargetProperty(scaleXAnim, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
        sb.Children.Add(scaleXAnim);

        var opacityAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = duration,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(opacityAnim, ShellGlobalSearchBox);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        sb.Children.Add(opacityAnim);

        sb.Completed += (_, _) =>
        {
            _isSearchAnimating = false;
            _isGlobalSearchExpanded = true;
        };
        sb.Begin();
    }

    private void CollapseGlobalSearch(bool force = false)
    {
        if (_isSearchAnimating || (!_isGlobalSearchExpanded && !force))
        {
            return;
        }

        if (!force && !string.IsNullOrWhiteSpace(ShellGlobalSearchBox.Text))
        {
            return;
        }

        _isSearchAnimating = true;
        var sb = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(150);

        var scaleXAnim = new DoubleAnimation
        {
            From = 1,
            To = CollapsedSearchBoxScale,
            Duration = duration,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(scaleXAnim, ShellGlobalSearchBox);
        Storyboard.SetTargetProperty(scaleXAnim, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
        sb.Children.Add(scaleXAnim);

        var opacityAnim = new DoubleAnimation
        {
            From = ShellGlobalSearchBox.Opacity <= 0 ? 1 : ShellGlobalSearchBox.Opacity,
            To = 0,
            Duration = duration,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(opacityAnim, ShellGlobalSearchBox);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        sb.Children.Add(opacityAnim);

        sb.Completed += (_, _) =>
        {
            _isSearchAnimating = false;
            _isGlobalSearchExpanded = false;
            try
            {
                App.Services.GetRequiredService<IGlobalSearchFlyout>().Close();
            }
            catch
            {
                // Service transient failure is non-fatal for UI collapse.
            }

            ShellGlobalSearchBox.Visibility = Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(ShellGlobalSearchBox.Text))
            {
                ShellGlobalSearchBox.Text = "";
            }
        };
        sb.Begin();
    }

    private static bool IsGlobalSearchShortcutTipSeen()
    {
        if (ApplicationData.Current.LocalSettings.Values.TryGetValue(GlobalSearchShortcutTipSettingsKey, out var v))
        {
            return v switch
            {
                true => true,
                string s when bool.TryParse(s, out var b) && b => true,
                _ => false,
            };
        }

        return false;
    }

    private static void MarkGlobalSearchShortcutTipSeen()
    {
        _globalSearchShortcutTipShownThisSession = true;
        ApplicationData.Current.LocalSettings.Values[GlobalSearchShortcutTipSettingsKey] = true;
    }
}

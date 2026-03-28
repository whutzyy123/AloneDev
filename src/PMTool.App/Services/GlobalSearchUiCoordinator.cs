using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using PMTool.App.Controls;
using PMTool.App.ViewModels;
using WinUiApplication = Microsoft.UI.Xaml.Application;

namespace PMTool.App.Services;

public sealed class GlobalSearchUiCoordinator : IGlobalSearchFlyout
{
    private readonly GlobalSearchViewModel _viewModel;
    private Flyout? _flyout;
    private GlobalSearchPanel? _panel;
    private FrameworkElement? _lastAnchor;

    public GlobalSearchUiCoordinator(GlobalSearchViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public bool IsOpen => _flyout?.IsOpen == true;

    public void TryOpen(FrameworkElement? anchor)
    {
        if (anchor?.XamlRoot is null)
        {
            return;
        }

        if (_lastAnchor is { } oldAnchor)
        {
            oldAnchor.SizeChanged -= OnSearchAnchorSizeChanged;
        }

        _lastAnchor = anchor;
        anchor.SizeChanged += OnSearchAnchorSizeChanged;

        _panel ??= new GlobalSearchPanel { DataContext = _viewModel };
        _panel.XamlRoot = anchor.XamlRoot;
        _panel.QueryFocusTarget = anchor;

        if (_flyout is null)
        {
            _flyout = new Flyout
            {
                Content = _panel,
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            };
            if (WinUiApplication.Current.Resources.TryGetValue("AloneGlobalSearchFlyoutPresenterStyle", out var fpObj)
                && fpObj is Style flyoutPresenterStyle)
            {
                _flyout.FlyoutPresenterStyle = flyoutPresenterStyle;
            }

            _flyout.Opened += OnFlyoutOpened;
            _flyout.Closed += OnFlyoutClosed;
        }

        // 已打开时避免重复 ShowAt（部分环境下会卡住输入栈）
        if (_flyout.IsOpen)
        {
            SyncPanelWidthToAnchor();
            EnqueueFocusQueryBox();
            return;
        }

        _flyout.ShowAt(anchor);
    }

    public void SyncHitFocusFromViewModel()
    {
        var idx = _viewModel.FocusedHitFlatIndex;
        _panel?.FocusHitByFlatIndex(idx);
    }

    private void OnSearchAnchorSizeChanged(object sender, SizeChangedEventArgs e)
    {
        SyncPanelWidthToAnchor();
    }

    private void SyncPanelWidthToAnchor()
    {
        if (_panel is null || _lastAnchor is null)
        {
            return;
        }

        var w = _lastAnchor.ActualWidth;
        if (w > 0)
        {
            _panel.ApplyFlyoutWidth(w);
        }
    }

    public void Close()
    {
        _flyout?.Hide();
    }

    /// <summary>供快捷键在延后调度后调用；实际聚焦在 Flyout Opened + Low 优先级中执行。</summary>
    public void FocusQueryWhenOpen() => EnqueueFocusQueryBox();

    private void EnqueueFocusQueryBox()
    {
        var p = _panel;
        if (p is null)
        {
            return;
        }

        var dq = p.DispatcherQueue;
        _ = dq.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_panel is null)
            {
                return;
            }

            _panel.FocusQueryBox();
        });
    }

    private void OnFlyoutOpened(object? sender, object e)
    {
        var dq = _panel?.DispatcherQueue;
        if (dq is null)
        {
            return;
        }

        _ = dq.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
            {
                SyncPanelWidthToAnchor();
                _panel?.FocusQueryBox();
            });
    }

    private void OnFlyoutClosed(object? sender, object e)
    {
        if (_lastAnchor is { } a)
        {
            a.SizeChanged -= OnSearchAnchorSizeChanged;
        }

        if (_panel is not null)
        {
            _panel.QueryFocusTarget = null;
        }

        _lastAnchor = null;
        _viewModel.OnFlyoutClosed();
    }
}

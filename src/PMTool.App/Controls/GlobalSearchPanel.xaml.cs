using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PMTool.App.Services;
using PMTool.App.ViewModels;
using Windows.System;

namespace PMTool.App.Controls;

public sealed partial class GlobalSearchPanel : UserControl
{
    /// <summary>x:Bind 根路径（与 MainShellPage+ShellViewModel 模式一致，避免 UserControl 根 x:DataType 指向 partial VM 时 Pass2 失败）。</summary>
    public GlobalSearchViewModel ViewModel { get; private set; } = null!;

    /// <summary>顶栏等与 ViewModel.Query 绑定的输入宿主；键盘「在搜索框内」判定用。</summary>
    public FrameworkElement? QueryFocusTarget { get; set; }

    public GlobalSearchPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnPanelKeyDown), handledEventsToo: true);
        KeyUp += OnKeyUp;
    }

    private void OnDataContextChanged(FrameworkElement sender, Microsoft.UI.Xaml.DataContextChangedEventArgs args)
    {
        if (DataContext is GlobalSearchViewModel vm)
        {
            ViewModel = vm;
        }
    }

    /// <summary>将下拉面板设为与锚点一致的内容宽度（由 <see cref="GlobalSearchUiCoordinator"/> 在布局后调用）。</summary>
    public void ApplyFlyoutWidth(double width)
    {
        if (width <= 0 || double.IsNaN(width))
        {
            return;
        }

        FlyoutRootBorder.Width = width;
    }

    public void FocusQueryBox()
    {
        if (QueryFocusTarget is Control c)
        {
            _ = c.Focus(FocusState.Programmatic);
        }
    }

    public void FocusHitByFlatIndex(int flatIndex)
    {
        if (flatIndex < 0 || ViewModel is null)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            var btn = FindHitButton(this, flatIndex);
            if (btn is not null)
            {
                _ = btn.Focus(FocusState.Programmatic);
                btn.StartBringIntoView();
            }
        });
    }

    private void HitResult_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not Button fe || fe.DataContext is not GlobalSearchHitRowViewModel row)
        {
            return;
        }

        ViewModel.SetFocusedHitFromUser(row.FlatIndex);
        ViewModel.OpenHitCommand.Execute(row.Hit);
    }

    private void HitRow_GotFocus(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not Control fe || fe.DataContext is not GlobalSearchHitRowViewModel row)
        {
            return;
        }

        ViewModel.SetFocusedHitFromUser(row.FlatIndex);
    }

    private void OnPanelKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel is null || XamlRoot is null)
        {
            return;
        }

        if (ViewModel.GetDisplayedHitCount() == 0)
        {
            return;
        }

        var focusedEl = FocusManager.GetFocusedElement(XamlRoot);
        var focusedDo = focusedEl as DependencyObject;
        var inQuery = QueryFocusTarget is not null && IsDescendantOf(focusedDo, QueryFocusTarget);

        switch (e.Key)
        {
            case VirtualKey.Down:
                e.Handled = true;
                if (inQuery)
                {
                    ViewModel.FocusFirstHit();
                }
                else
                {
                    ViewModel.MoveFocusedHit(1);
                }

                AfterHitNavigated();
                return;

            case VirtualKey.Up:
                if (inQuery)
                {
                    return;
                }

                e.Handled = true;
                ViewModel.MoveFocusedHit(-1);
                if (ViewModel.FocusedHitFlatIndex < 0)
                {
                    FocusQueryBox();
                }
                else
                {
                    AfterHitNavigated();
                }

                return;

            case VirtualKey.Home:
                if (inQuery)
                {
                    return;
                }

                e.Handled = true;
                ViewModel.FocusFirstHit();
                AfterHitNavigated();
                return;

            case VirtualKey.End:
                if (inQuery)
                {
                    return;
                }

                e.Handled = true;
                ViewModel.FocusLastHit();
                AfterHitNavigated();
                return;
        }
    }

    private void AfterHitNavigated()
    {
        if (ViewModel.FocusedHitFlatIndex < 0)
        {
            return;
        }

        FocusHitByFlatIndex(ViewModel.FocusedHitFlatIndex);
    }

    private static bool IsDescendantOf(DependencyObject? node, DependencyObject? ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private static Button? FindHitButton(DependencyObject root, int flatIndex)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button b
                && b.DataContext is GlobalSearchHitRowViewModel r
                && r.FlatIndex == flatIndex)
            {
                return b;
            }

            var found = FindHitButton(child, flatIndex);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private async void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            var flyout = App.Services.GetRequiredService<IGlobalSearchFlyout>();
            flyout.Close();
            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await ViewModel.TryHandleEnterForSelectionAsync().ConfigureAwait(true);
    }
}

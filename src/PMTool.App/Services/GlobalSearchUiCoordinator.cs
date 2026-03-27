using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using PMTool.App.Controls;

namespace PMTool.App.Services;

/// <summary>Hosts global search flyout; debounced query wiring comes later (PRD 6.10).</summary>
public sealed class GlobalSearchUiCoordinator
{
    private Flyout? _flyout;

    public void TryOpen(FrameworkElement? anchor)
    {
        if (anchor is null)
        {
            return;
        }

        _flyout ??= CreateFlyout();
        _flyout.ShowAt(anchor);
    }

    private static Flyout CreateFlyout()
    {
        var panel = new GlobalSearchPanel();
        return new Flyout
        {
            Content = panel,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
        };
    }
}

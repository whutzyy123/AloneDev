using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using WinUiApplication = Microsoft.UI.Xaml.Application;

namespace PMTool.App.UI;

/// <summary>
/// 统一 ContentDialog 圆角、主次按钮样式与危险主操作色（资源见 Themes/Dialogs.xaml）。
/// </summary>
public static class AloneDialogFactory
{
    private static readonly System.Uri FormFieldResourcesUri = new("ms-appx:///Themes/DialogFormFieldResources.xaml");

    public static Thickness DialogContentPadding
    {
        get
        {
            if (WinUiApplication.Current?.Resources.TryGetValue("AloneDialogContentPadding", out var v) == true && v is Thickness t)
            {
                return t;
            }

            return new Thickness(24, 20, 24, 16);
        }
    }

    /// <summary>对话框内 StackPanel 等纵向间距（资源键 AloneDialogFormSpacing）。</summary>
    public static double DialogFormSpacing
    {
        get
        {
            if (WinUiApplication.Current?.Resources.TryGetValue("AloneDialogFormSpacing", out var v) == true && v is double d)
            {
                return d;
            }

            return 16;
        }
    }

    private static Style? GetStyle(string key) =>
        WinUiApplication.Current?.Resources.TryGetValue(key, out var o) == true ? o as Style : null;

    public static void ApplyFormTextBoxStyle(TextBox box)
    {
        if (GetStyle("AloneFormTextBoxStyle") is { } st)
        {
            box.Style = st;
        }
    }

    public static void ApplyFormNumberBoxStyle(NumberBox box)
    {
        if (GetStyle("AloneFormNumberBoxStyle") is { } st)
        {
            box.Style = st;
        }
    }

    /// <summary>主/次/关闭按钮样式与对话框壳（圆角）。</summary>
    public static void ApplyChrome(ContentDialog d)
    {
        if (GetStyle("AloneContentDialogStyle") is { } shell)
        {
            d.Style = shell;
        }

        if (GetStyle("AloneDialogPrimaryButtonStyle") is { } primary)
        {
            d.PrimaryButtonStyle = primary;
        }

        if (GetStyle("AloneDialogCloseButtonStyle") is { } close)
        {
            d.CloseButtonStyle = close;
        }

        if (GetStyle("AloneDialogSecondaryButtonStyle") is { } secondary)
        {
            d.SecondaryButtonStyle = secondary;
        }

        AttachFormFieldResourceDictionary(d);
        d.Opened += OnDialogOpened;
    }

    public static void ApplyDestructivePrimary(ContentDialog d)
    {
        if (GetStyle("AloneDialogDestructivePrimaryButtonStyle") is { } ds)
        {
            d.PrimaryButtonStyle = ds;
        }
    }

    private static void AttachFormFieldResourceDictionary(ContentDialog d)
    {
        if (d.Content is not FrameworkElement root)
        {
            return;
        }

        var merge = new ResourceDictionary { Source = FormFieldResourcesUri };
        root.Resources.MergedDictionaries.Add(merge);
    }

    private static void OnDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        sender.Opened -= OnDialogOpened;
        _ = sender.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            TryApplySmokeLayer(sender);
            TryRelaxDialogCommandColumns(sender);
            TryAnimateDialogEntrance(sender);
        });
    }

    private static void TryAnimateDialogEntrance(ContentDialog dialog)
    {
        if (FindNamedBorder(dialog, "BackgroundElement", 0) is not { } chrome)
        {
            return;
        }

        chrome.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        var scale = new ScaleTransform { ScaleX = 0.96, ScaleY = 0.96 };
        chrome.RenderTransform = scale;
        chrome.Opacity = 0;

        var sb = new Storyboard();
        var dur = System.TimeSpan.FromMilliseconds(200);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var sx = new DoubleAnimation
        {
            From = 0.96,
            To = 1,
            Duration = dur,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(sx, chrome);
        Storyboard.SetTargetProperty(sx, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
        sb.Children.Add(sx);

        var sy = new DoubleAnimation
        {
            From = 0.96,
            To = 1,
            Duration = dur,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(sy, chrome);
        Storyboard.SetTargetProperty(sy, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
        sb.Children.Add(sy);

        var op = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = dur,
            EasingFunction = ease,
        };
        Storyboard.SetTarget(op, chrome);
        Storyboard.SetTargetProperty(op, "Opacity");
        sb.Children.Add(op);

        sb.Begin();
    }

    private static Border? FindNamedBorder(DependencyObject node, string name, int depth)
    {
        if (depth > 40)
        {
            return null;
        }

        if (node is Border b && b.Name == name)
        {
            return b;
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            var found = FindNamedBorder(child, name, depth + 1);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static void TryApplySmokeLayer(DependencyObject root)
    {
        WalkForSmoke(root, CreateDialogSmokeBrush());
    }

    /// <summary>
    /// WinUI 3 的 <see cref="AcrylicBrush"/> 无 BackgroundSource，此处用应用内亚克力主题刷近似磨砂（胜于纯纯色压暗）。
    /// </summary>
    private static Brush CreateDialogSmokeBrush()
    {
        var app = WinUiApplication.Current;
        if (app?.Resources.TryGetValue("AcrylicBackgroundFillColorDefaultBrush", out var bg) == true && bg is Brush bgBrush)
        {
            return bgBrush;
        }

        if (app?.Resources.TryGetValue("AcrylicInAppFillColorDefaultBrush", out var o) == true && o is Brush b)
        {
            return b;
        }

        var light = app?.RequestedTheme != ApplicationTheme.Dark;
        return new SolidColorBrush(light ? Color.FromArgb(102, 245, 247, 250) : Color.FromArgb(140, 12, 13, 18));
    }

    private static void WalkForSmoke(DependencyObject node, Brush brush)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is Rectangle { Name: "SmokeLayerBackground" } r)
            {
                r.Fill = brush;
                return;
            }

            WalkForSmoke(child, brush);
        }
    }

    /// <summary>
    /// WinUI 默认命令区多为两列等宽；将首列改为 Auto、末列 Star，使主按钮占满剩余宽度。
    /// </summary>
    private static void TryRelaxDialogCommandColumns(DependencyObject root)
    {
        WalkForCommandGrid(root);
    }

    private static void WalkForCommandGrid(DependencyObject node)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is Grid g && TryPatchCommandGridColumns(g))
            {
                return;
            }

            WalkForCommandGrid(child);
        }
    }

    private static bool TryPatchCommandGridColumns(Grid grid)
    {
        var defs = grid.ColumnDefinitions;
        if (defs.Count == 3
            && defs[1].Width.GridUnitType == GridUnitType.Pixel
            && System.Math.Abs(defs[1].Width.Value - 8) < 0.01)
        {
            defs[0].Width = GridLength.Auto;
            defs[2].Width = new GridLength(1, GridUnitType.Star);
            return true;
        }

        if (defs.Count == 2 && ButtonChildCount(grid) >= 2)
        {
            defs[0].Width = GridLength.Auto;
            defs[1].Width = new GridLength(1, GridUnitType.Star);
            return true;
        }

        return false;
    }

    private static int ButtonChildCount(Panel grid)
    {
        var n = 0;
        foreach (var c in grid.Children)
        {
            if (c is Button)
            {
                n++;
            }
        }

        return n;
    }

    public static ContentDialog CreateStandard(
        XamlRoot xamlRoot,
        string title,
        object content,
        string primaryButtonText,
        string closeButtonText = "取消")
    {
        var d = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
        };
        ApplyChrome(d);
        return d;
    }

    public static ContentDialog CreateStandardWithSecondary(
        XamlRoot xamlRoot,
        string title,
        object content,
        string primaryButtonText,
        string secondaryButtonText,
        ContentDialogButton defaultButton,
        string? closeButtonText = null)
    {
        var d = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            DefaultButton = defaultButton,
        };
        if (!string.IsNullOrEmpty(closeButtonText))
        {
            d.CloseButtonText = closeButtonText;
        }

        ApplyChrome(d);
        return d;
    }

    /// <summary>不可逆操作：默认焦点在「取消」，主按钮为错误色。</summary>
    public static ContentDialog CreateDestructiveConfirm(
        XamlRoot xamlRoot,
        string title,
        object content,
        string primaryButtonText,
        string closeButtonText = "取消")
    {
        var d = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
        };
        ApplyChrome(d);
        ApplyDestructivePrimary(d);
        return d;
    }

    /// <summary>仅确认：单「关闭」按钮（错误提示等）。</summary>
    public static ContentDialog CreateInfo(XamlRoot xamlRoot, string title, object content, string closeButtonText = "关闭")
    {
        var d = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = content,
            CloseButtonText = closeButtonText,
        };
        ApplyChrome(d);
        return d;
    }
}

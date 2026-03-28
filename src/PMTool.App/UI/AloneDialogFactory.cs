using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUiApplication = Microsoft.UI.Xaml.Application;

namespace PMTool.App.UI;

/// <summary>
/// 统一 ContentDialog 圆角、主次按钮样式与危险主操作色（资源见 Themes/Dialogs.xaml）。
/// </summary>
public static class AloneDialogFactory
{
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
    }

    public static void ApplyDestructivePrimary(ContentDialog d)
    {
        if (GetStyle("AloneDialogDestructivePrimaryButtonStyle") is { } ds)
        {
            d.PrimaryButtonStyle = ds;
        }
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

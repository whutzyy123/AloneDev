using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinRT.Interop;

namespace PMTool.App.UI;

/// <summary>主窗口 Mica 与扩展到标题栏，消除默认实色标题栏与主界面的割裂感。</summary>
public static class WindowChromeHelper
{
    public static void ApplyMicaAndTitleBar(Window? window, UIElement titleBarDragTarget)
    {
        if (window is null)
        {
            return;
        }

        try
        {
            window.SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            // 低版本系统或受限环境：忽略
        }

        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            try
            {
                appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            }
            catch
            {
                // 旧版 SDK / 部分环境不可用
            }

            appWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(0, 255, 255, 255);
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0, 255, 255, 255);
            var fg = CaptionButtonForeground(window);
            appWindow.TitleBar.ButtonForegroundColor = fg;
            appWindow.TitleBar.ButtonInactiveForegroundColor = fg;
        }
        catch
        {
            // 设计器 / 无 HWND
        }

        try
        {
            window.SetTitleBar(titleBarDragTarget);
        }
        catch
        {
            // 忽略
        }
    }

    private static Color CaptionButtonForeground(Window window)
    {
        if (window.Content is FrameworkElement fe)
        {
            return fe.ActualTheme == ElementTheme.Dark
                ? Color.FromArgb(255, 245, 245, 250)
                : Color.FromArgb(255, 28, 28, 32);
        }

        return Color.FromArgb(255, 28, 28, 32);
    }
}

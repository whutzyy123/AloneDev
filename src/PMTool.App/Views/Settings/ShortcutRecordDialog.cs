using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using App = Microsoft.UI.Xaml.Application;
using PMTool.App.Services;
using PMTool.App.UI;
using Windows.System;

namespace PMTool.App.Views.Settings;

public static class ShortcutRecordDialog
{
    public static async Task<string?> PickAsync(XamlRoot xamlRoot, string? initialDisplay)
    {
        var preview = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(initialDisplay) ? "—" : initialDisplay,
        };

        var error = new TextBlock
        {
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed),
            TextWrapping = TextWrapping.WrapWholeWords,
            Visibility = Visibility.Collapsed,
        };

        string? lastValid = null;
        var host = new Border
        {
            Background = (Brush)App.Current.Resources["AloneSurfaceContainerHighestBrush"],
            CornerRadius = (CornerRadius)App.Current.Resources["DefaultCornerRadius"],
            Height = 88,
            IsTabStop = true,
            TabIndex = 0,
            Child = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "点此区域后按下组合键",
            },
        };

        host.KeyDown += (_, e) =>
        {
            error.Visibility = Visibility.Collapsed;
            error.Text = "";

            if (e.Key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
                or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
                or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
                or VirtualKey.LeftWindows or VirtualKey.RightWindows)
            {
                e.Handled = true;
                return;
            }

            var mods = VirtualKeyModifiers.None;
            if (IsDown(VirtualKey.Control))
            {
                mods |= VirtualKeyModifiers.Control;
            }

            if (IsDown(VirtualKey.Shift))
            {
                mods |= VirtualKeyModifiers.Shift;
            }

            if (IsDown(VirtualKey.Menu))
            {
                mods |= VirtualKeyModifiers.Menu;
            }

            if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows))
            {
                mods |= VirtualKeyModifiers.Windows;
            }

            if (mods == VirtualKeyModifiers.None)
            {
                error.Text = "需配合 Ctrl / Shift / Alt / Win 使用。";
                error.Visibility = Visibility.Visible;
                e.Handled = true;
                return;
            }

            var display = ShortcutBindingParser.Format(e.Key, mods);
            if (!ShortcutBindingParser.TryNormalizeDisplay(display, out var norm, out var errMsg))
            {
                error.Text = errMsg ?? "无效组合。";
                error.Visibility = Visibility.Visible;
                e.Handled = true;
                return;
            }

            preview.Text = norm;
            lastValid = norm;
            e.Handled = true;
        };

        var panel = new StackPanel
        {
            Spacing = AloneDialogFactory.DialogFormSpacing,
            Padding = AloneDialogFactory.DialogContentPadding,
        };
        panel.Children.Add(new TextBlock
        {
            Text = "录制后点「确定」写入；取消则不修改。",
            TextWrapping = TextWrapping.WrapWholeWords,
        });
        panel.Children.Add(host);
        panel.Children.Add(preview);
        panel.Children.Add(error);

        var dialog = AloneDialogFactory.CreateStandard(xamlRoot, "录制快捷键", panel, "确定");

        dialog.Opened += (_, _) => _ = host.Focus(FocusState.Programmatic);

        var r = await dialog.ShowAsync();
        return r == ContentDialogResult.Primary ? lastValid ?? initialDisplay : null;
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(VirtualKeyStates.Down);
}

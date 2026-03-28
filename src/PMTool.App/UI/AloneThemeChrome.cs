using Microsoft.UI.Xaml.Controls;
using WinUiApplication = Microsoft.UI.Xaml.Application;

namespace PMTool.App.UI;

/// <summary>在代码里创建的控件上套用主题 Style（应用级隐式 Style 会触发 WinUI 标记编译器误报 WMC1506，故显式调用）。</summary>
public static class AloneThemeChrome
{
    public static void ApplyComboBoxStyle(ComboBox box)
    {
        if (WinUiApplication.Current?.Resources.TryGetValue("AloneComboBoxStyle", out var o) == true && o is Microsoft.UI.Xaml.Style st)
        {
            box.Style = st;
        }
    }
}

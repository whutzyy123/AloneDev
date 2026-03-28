using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PMTool.App.UI;

/// <summary>对话框需在可视化树上呈现；Page 尚未完成加载时 XamlRoot 可能为 null，回退到主窗口内容。</summary>
internal static class XamlRootResolver
{
    public static XamlRoot? ForPage(Page page)
    {
        if (page.XamlRoot is not null)
        {
            return page.XamlRoot;
        }

        return global::PMTool.App.App.MainWindow?.Content is UIElement el ? el.XamlRoot : null;
    }
}

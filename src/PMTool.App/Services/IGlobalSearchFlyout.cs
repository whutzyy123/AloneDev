namespace PMTool.App.Services;

public interface IGlobalSearchFlyout
{
    bool IsOpen { get; }

    void Close();

    void TryOpen(Microsoft.UI.Xaml.FrameworkElement? anchor);

    void FocusQueryWhenOpen();

    /// <summary>键盘选中后同步焦点到对应命中行按钮（命中区位于 Flyout 内）。</summary>
    void SyncHitFocusFromViewModel();
}

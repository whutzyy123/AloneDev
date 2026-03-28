namespace PMTool.App.Services;

/// <summary>设置页保存快捷键后通知主导航页刷新 <see cref="MainShellShortcutController"/>。</summary>
public static class MainShellShortcutReload
{
    public static Action? RequestReload { get; set; }
}

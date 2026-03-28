namespace PMTool.Core.Models.Settings;

/// <summary>PRD 6.9.4 默认快捷键显示串。</summary>
public static class AppShortcutDefaults
{
    public const string NewProject = "Ctrl+Shift+P";
    public const string NewFeature = "Ctrl+N";
    public const string NewTask = "Ctrl+Shift+T";
    public const string NewDocument = "Ctrl+Shift+D";
    public const string NewIdea = "Ctrl+Shift+I";
    public const string GlobalSearch = "Ctrl+K";
    public const string Save = "Ctrl+S";
    public const string Undo = "Ctrl+Z";
    public const string Redo = "Ctrl+Y";

    public static AppConfiguration WithDefaultShortcuts(AppConfiguration cfg)
    {
        void SetIfEmpty(string key, string value)
        {
            if (!cfg.Shortcuts.ContainsKey(key))
            {
                cfg.Shortcuts[key] = value;
            }
        }

        SetIfEmpty(nameof(ShortcutActionId.NewProject), NewProject);
        SetIfEmpty(nameof(ShortcutActionId.NewFeature), NewFeature);
        SetIfEmpty(nameof(ShortcutActionId.NewTask), NewTask);
        SetIfEmpty(nameof(ShortcutActionId.NewDocument), NewDocument);
        SetIfEmpty(nameof(ShortcutActionId.NewIdea), NewIdea);
        SetIfEmpty(nameof(ShortcutActionId.GlobalSearch), GlobalSearch);
        SetIfEmpty(nameof(ShortcutActionId.Save), Save);
        SetIfEmpty(nameof(ShortcutActionId.Undo), Undo);
        SetIfEmpty(nameof(ShortcutActionId.Redo), Redo);
        cfg.Shortcuts[nameof(ShortcutActionId.GlobalSearch)] = GlobalSearch;
        return cfg;
    }
}

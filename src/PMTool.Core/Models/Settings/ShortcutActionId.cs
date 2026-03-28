namespace PMTool.Core.Models.Settings;

/// <summary>与 PRD 6.9.4 Shortcuts 键一致；<see cref="ShortcutActionId.GlobalSearch"/> 保存时强制为 Ctrl+K。</summary>
public enum ShortcutActionId
{
    NewProject,
    NewFeature,
    NewTask,
    NewDocument,
    NewIdea,
    GlobalSearch,
    Save,
    Undo,
    Redo,
}

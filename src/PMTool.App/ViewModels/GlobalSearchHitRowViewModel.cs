using CommunityToolkit.Mvvm.ComponentModel;
using PMTool.Core.Models.Search;

namespace PMTool.App.ViewModels;

/// <summary>仅在搜索面板中用于 x:DataType（避免跨程序集模型直接进入 DataTemplate 导致 Pass2 失败）。</summary>
public partial class GlobalSearchHitRowViewModel : ObservableObject
{
    public GlobalSearchHitRowViewModel(GlobalSearchHit hit, string? highlightNeedle)
    {
        Hit = hit;
        HighlightNeedle = string.IsNullOrEmpty(highlightNeedle) ? null : highlightNeedle;
    }

    public GlobalSearchHit Hit { get; }

    /// <summary>供摘要关键词弱化高亮（与全局搜索有效关键词一致）。</summary>
    public string? HighlightNeedle { get; }

    [ObservableProperty]
    private int _flatIndex = -1;

    [ObservableProperty]
    private bool _isKeyboardHighlighted;

    public string Title => Hit.Title;

    public string Snippet => Hit.Snippet;

    public string UpdatedAt => Hit.UpdatedAt;
}

using CommunityToolkit.Mvvm.ComponentModel;
using PMTool.Core.Models.Search;

namespace PMTool.App.ViewModels;

/// <summary>仅在搜索面板中用于 x:DataType（避免跨程序集模型直接进入 DataTemplate 导致 Pass2 失败）。</summary>
public partial class GlobalSearchHitRowViewModel : ObservableObject
{
    public GlobalSearchHitRowViewModel(GlobalSearchHit hit) => Hit = hit;

    public GlobalSearchHit Hit { get; }

    [ObservableProperty]
    private int _flatIndex = -1;

    [ObservableProperty]
    private bool _isKeyboardHighlighted;

    public string Title => Hit.Title;

    public string Snippet => Hit.Snippet;

    public string UpdatedAt => Hit.UpdatedAt;
}

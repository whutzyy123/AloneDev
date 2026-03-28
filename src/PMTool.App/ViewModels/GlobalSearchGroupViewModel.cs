using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PMTool.Core.Models.Search;

namespace PMTool.App.ViewModels;

public partial class GlobalSearchGroupViewModel : ObservableObject
{
    private readonly List<GlobalSearchHit> _all;
    private readonly string? _highlightNeedle;

    /// <summary>DisplayedHits 重建后触发，供 <see cref="GlobalSearchViewModel"/> 扁平编号与键盘焦点校正。</summary>
    public event EventHandler? DisplayedHitsRebuilt;

    public GlobalSearchGroupViewModel(GlobalSearchModule module, List<GlobalSearchHit> hits, string? highlightNeedle)
    {
        Module = module;
        _all = hits;
        _highlightNeedle = string.IsNullOrEmpty(highlightNeedle) ? null : highlightNeedle;
        Title = ModuleToLabel(module);
        RefreshDisplayed();
    }

    public GlobalSearchModule Module { get; }

    public string Title { get; }

    public ObservableCollection<GlobalSearchHitRowViewModel> DisplayedHits { get; } = [];

    public int TotalCount => _all.Count;

    public bool ShowMoreChevron => TotalCount > 5;

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        RefreshDisplayed();
        OnPropertyChanged(nameof(ExpandToggleText));
    }

    public string ExpandToggleText => IsExpanded ? "收起" : $"查看更多（共 {TotalCount} 条）";

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;

    private void RefreshDisplayed()
    {
        DisplayedHits.Clear();
        var take = IsExpanded ? _all.Count : Math.Min(5, _all.Count);
        foreach (var h in _all.Take(take))
        {
            DisplayedHits.Add(new GlobalSearchHitRowViewModel(h, _highlightNeedle));
        }

        DisplayedHitsRebuilt?.Invoke(this, EventArgs.Empty);
    }

    private static string ModuleToLabel(GlobalSearchModule m) => m switch
    {
        GlobalSearchModule.Project => "项目",
        GlobalSearchModule.Feature => "模块",
        GlobalSearchModule.Task => "任务",
        GlobalSearchModule.Document => "文档",
        GlobalSearchModule.Idea => "灵感池",
        _ => "其他",
    };
}

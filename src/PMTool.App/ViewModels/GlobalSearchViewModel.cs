using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using PMTool.App.Services;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models.Search;

namespace PMTool.App.ViewModels;

public partial class GlobalSearchViewModel(
    IGlobalSearchRepository repository,
    IGlobalSearchNavigationService navigation) : ObservableObject
{
    /// <summary>全局搜索浮层为固定高度无滚动，单模块条数宜少，避免截断大量无效结果。</summary>
    private const int PerModuleCap = 6;
    private DispatcherQueueTimer? _debounceTimer;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private bool _scopeProjects = true;

    [ObservableProperty]
    private bool _scopeFeatures = true;

    [ObservableProperty]
    private bool _scopeTasks = true;

    [ObservableProperty]
    private bool _scopeDocuments = true;

    [ObservableProperty]
    private bool _scopeIdeas = true;

    /// <summary>有效关键词（过滤特殊字符后），用于摘要高亮。</summary>
    [ObservableProperty]
    private string _activeNeedle = "";

    [ObservableProperty]
    private string _filterHint = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _showPerformanceHint;

    [ObservableProperty]
    private string _keywordForHighlight = "";

    /// <summary>扁平结果序号（当前 <see cref="GlobalSearchGroupViewModel.DisplayedHits"/> 并集）。-1 表示无键盘高亮。</summary>
    [ObservableProperty]
    private int _focusedHitFlatIndex = -1;

    public ObservableCollection<GlobalSearchGroupViewModel> Groups { get; } = [];

    partial void OnFocusedHitFlatIndexChanged(int value) => ApplyHitRowHighlights();

    partial void OnQueryChanged(string value) => ScheduleSearch();

    partial void OnScopeProjectsChanged(bool value) => ScheduleSearchImmediate();

    partial void OnScopeFeaturesChanged(bool value) => ScheduleSearchImmediate();

    partial void OnScopeTasksChanged(bool value) => ScheduleSearchImmediate();

    partial void OnScopeDocumentsChanged(bool value) => ScheduleSearchImmediate();

    partial void OnScopeIdeasChanged(bool value) => ScheduleSearchImmediate();

    public void OnFlyoutClosed()
    {
        _searchCts?.Cancel();
        _searchCts = null;
        FocusedHitFlatIndex = -1;
    }

    private void ScheduleSearch()
    {
        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is null)
        {
            return;
        }

        _debounceTimer ??= dq.CreateTimer();
        _debounceTimer.Interval = TimeSpan.FromMilliseconds(500);
        _debounceTimer.IsRepeating = false;
        _debounceTimer.Tick -= OnDebounceTick;
        _debounceTimer.Tick += OnDebounceTick;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private async void OnDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnDebounceTick;
        await ExecuteSearchAsync().ConfigureAwait(true);
    }

    private void ScheduleSearchImmediate()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            return;
        }

        _ = ExecuteSearchAsync();
    }

    private GlobalSearchScope BuildScope()
    {
        var s = GlobalSearchScope.None;
        if (ScopeProjects)
        {
            s |= GlobalSearchScope.Projects;
        }

        if (ScopeFeatures)
        {
            s |= GlobalSearchScope.Features;
        }

        if (ScopeTasks)
        {
            s |= GlobalSearchScope.Tasks;
        }

        if (ScopeDocuments)
        {
            s |= GlobalSearchScope.Documents;
        }

        if (ScopeIdeas)
        {
            s |= GlobalSearchScope.Ideas;
        }

        return s;
    }

    public async Task ExecuteSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        var (needle, hadFilter) = GlobalSearchKeywordNormalizer.Normalize(Query);
        FilterHint = hadFilter && needle is { Length: > 0 }
            ? $"已过滤特殊字符，实际关键词：{needle}"
            : hadFilter && needle is null
                ? "关键词仅含非法字符，未执行搜索。"
                : "";

        ActiveNeedle = needle ?? "";
        KeywordForHighlight = needle ?? "";

        if (string.IsNullOrEmpty(needle))
        {
            IsSearching = false;
            Groups.Clear();
            FocusedHitFlatIndex = -1;
            StatusMessage = string.IsNullOrWhiteSpace(Query) ? "" : "请输入有效关键词。";
            ShowPerformanceHint = false;
            return;
        }

        var scope = BuildScope();
        if (scope == GlobalSearchScope.None)
        {
            Groups.Clear();
            FocusedHitFlatIndex = -1;
            StatusMessage = "请至少选择一个搜索范围。";
            IsSearching = false;
            return;
        }

        IsSearching = true;
        StatusMessage = "搜索中…";
        ShowPerformanceHint = false;

        try
        {
            var req = new GlobalSearchRequest(needle, scope, PerModuleCap);
            var resp = await repository.SearchAsync(req, ct).ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();

            ShowPerformanceHint = resp.ShowPerformanceHint;
            var byModule = resp.Hits
                .GroupBy(h => h.Module)
                .OrderBy(g => ModuleOrder(g.Key))
                .ToList();

            Groups.Clear();
            var total = 0;
            foreach (var g in byModule)
            {
                var sorted = g.OrderByDescending(x => x.MatchScore).ThenByDescending(x => x.UpdatedAt, StringComparer.Ordinal).ToList();
                if (sorted.Count == 0)
                {
                    continue;
                }

                total += sorted.Count;
                var gvm = new GlobalSearchGroupViewModel(g.Key, sorted);
                gvm.DisplayedHitsRebuilt += OnGroupDisplayedHitsRebuilt;
                Groups.Add(gvm);
            }

            FinalizeSearchResultFocus();

            StatusMessage = total == 0 ? "无匹配内容，请调整关键词或搜索范围。" : $"找到 {total} 条结果 · {resp.ElapsedMilliseconds} ms";
            if (resp.ShowPerformanceHint)
            {
                StatusMessage += " · 内容较多，若稍慢请耐心等待。";
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            Groups.Clear();
        }
        finally
        {
            IsSearching = false;
        }
    }

    private static int ModuleOrder(GlobalSearchModule m) => m switch
    {
        GlobalSearchModule.Project => 0,
        GlobalSearchModule.Feature => 1,
        GlobalSearchModule.Task => 2,
        GlobalSearchModule.Document => 3,
        GlobalSearchModule.Idea => 4,
        _ => 9,
    };

    [RelayCommand]
    private async Task OpenHitAsync(GlobalSearchHit? hit)
    {
        if (hit is null)
        {
            return;
        }

        try
        {
            await navigation.NavigateToHitAsync(hit).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    /// <summary>
    /// Enter：优先打开 <see cref="FocusedHitFlatIndex"/> 对应项；若为 -1 则打开首条展示结果（与常见搜索框一致）。
    /// </summary>
    public async Task TryHandleEnterForSelectionAsync()
    {
        var hit = FocusedHitFlatIndex >= 0 ? GetHitByFlatIndex(FocusedHitFlatIndex) : GetFirstHitOrDefault();
        await OpenHitAsync(hit).ConfigureAwait(true);
    }

    public int GetDisplayedHitCount() => CountFlatHits();

    public void MoveFocusedHit(int delta)
    {
        var n = CountFlatHits();
        if (n == 0)
        {
            return;
        }

        if (FocusedHitFlatIndex < 0 && delta > 0)
        {
            FocusedHitFlatIndex = 0;
            return;
        }

        if (FocusedHitFlatIndex < 0 && delta < 0)
        {
            FocusedHitFlatIndex = n - 1;
            return;
        }

        if (FocusedHitFlatIndex == 0 && delta < 0)
        {
            FocusedHitFlatIndex = -1;
            return;
        }

        var next = FocusedHitFlatIndex + delta;
        FocusedHitFlatIndex = Math.Clamp(next, 0, n - 1);
    }

    public void FocusFirstHit()
    {
        if (CountFlatHits() == 0)
        {
            return;
        }

        FocusedHitFlatIndex = 0;
    }

    public void FocusLastHit()
    {
        var n = CountFlatHits();
        if (n == 0)
        {
            return;
        }

        FocusedHitFlatIndex = n - 1;
    }

    public void SetFocusedHitFromUser(int flatIndex)
    {
        var n = CountFlatHits();
        if (n == 0 || flatIndex < 0 || flatIndex >= n)
        {
            return;
        }

        FocusedHitFlatIndex = flatIndex;
    }

    private void OnGroupDisplayedHitsRebuilt(object? sender, EventArgs e)
    {
        ReindexFlatHits();
        ClampFocusedFlatIndex();
        ApplyHitRowHighlights();
    }

    private void FinalizeSearchResultFocus()
    {
        ReindexFlatHits();
        FocusedHitFlatIndex = CountFlatHits() > 0 ? 0 : -1;
    }

    private int CountFlatHits()
    {
        var n = 0;
        foreach (var g in Groups)
        {
            n += g.DisplayedHits.Count;
        }

        return n;
    }

    private void ReindexFlatHits()
    {
        var idx = 0;
        foreach (var g in Groups)
        {
            foreach (var row in g.DisplayedHits)
            {
                row.FlatIndex = idx++;
            }
        }
    }

    private void ClampFocusedFlatIndex()
    {
        var n = CountFlatHits();
        if (n == 0)
        {
            FocusedHitFlatIndex = -1;
            return;
        }

        if (FocusedHitFlatIndex >= n)
        {
            FocusedHitFlatIndex = n - 1;
        }
    }

    private void ApplyHitRowHighlights()
    {
        var f = FocusedHitFlatIndex;
        foreach (var g in Groups)
        {
            foreach (var row in g.DisplayedHits)
            {
                row.IsKeyboardHighlighted = f >= 0 && row.FlatIndex == f;
            }
        }
    }

    private GlobalSearchHit? GetHitByFlatIndex(int index)
    {
        if (index < 0)
        {
            return null;
        }

        foreach (var g in Groups)
        {
            foreach (var row in g.DisplayedHits)
            {
                if (row.FlatIndex == index)
                {
                    return row.Hit;
                }
            }
        }

        return null;
    }

    /// <summary>供面板键盘导航记录当前高亮命中（简化为首组首条可由视图传入）。</summary>
    public GlobalSearchHit? GetFirstHitOrDefault()
    {
        foreach (var g in Groups)
        {
            if (g.DisplayedHits.Count > 0)
            {
                return g.DisplayedHits[0].Hit;
            }
        }

        return null;
    }
}

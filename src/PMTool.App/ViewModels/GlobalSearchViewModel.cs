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
    /// <summary>全局搜索单模块请求上限（与浮层说明、分组「查看更多」一致）。</summary>
    public const int PerModuleCap = 6;
    private DispatcherQueueTimer? _debounceTimer;
    private CancellationTokenSource? _searchCts;
    private bool _suppressScopeChanged;

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

    [ObservableProperty]
    private string _scopeTagText = "";

    public bool HasScopeTag => !string.IsNullOrWhiteSpace(ScopeTagText);

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

    /// <summary>供搜索说明折叠区展示，与 <see cref="PerModuleCap"/> 同步。</summary>
    public string MaxHitsPerCategoryHint => $"每个分组最多展示 {PerModuleCap} 条命中；若较多，可展开「查看更多」。";

    partial void OnFocusedHitFlatIndexChanged(int value) => ApplyHitRowHighlights();

    partial void OnQueryChanged(string value) => ScheduleSearch();

    partial void OnScopeTagTextChanged(string value) => OnPropertyChanged(nameof(HasScopeTag));

    partial void OnScopeProjectsChanged(bool value) => HandleScopeChanged();

    partial void OnScopeFeaturesChanged(bool value) => HandleScopeChanged();

    partial void OnScopeTasksChanged(bool value) => HandleScopeChanged();

    partial void OnScopeDocumentsChanged(bool value) => HandleScopeChanged();

    partial void OnScopeIdeasChanged(bool value) => HandleScopeChanged();

    private void HandleScopeChanged()
    {
        if (_suppressScopeChanged)
        {
            return;
        }

        ScopeTagText = "";
        ScheduleSearchImmediate();
    }

    public void ApplyCurrentModuleScope(string navKey, string moduleTitle)
    {
        var key = (navKey ?? string.Empty).Trim().ToLowerInvariant();
        switch (key)
        {
            case "projects":
                SetScopeWithTag(moduleTitle, projects: true, features: false, tasks: false, documents: false, ideas: false);
                break;
            case "features":
                SetScopeWithTag(moduleTitle, projects: false, features: true, tasks: false, documents: false, ideas: false);
                break;
            case "tasks":
                SetScopeWithTag(moduleTitle, projects: false, features: false, tasks: true, documents: false, ideas: false);
                break;
            case "documents":
            case "snippets":
                SetScopeWithTag(moduleTitle, projects: false, features: false, tasks: false, documents: true, ideas: false);
                break;
            case "ideas":
                SetScopeWithTag(moduleTitle, projects: false, features: false, tasks: false, documents: false, ideas: true);
                break;
            case "releases":
                SetScopeWithTag(moduleTitle, projects: true, features: true, tasks: true, documents: false, ideas: false);
                break;
            default:
                // data/settings 等非列表模块：恢复全库范围，不展示当前模块标签
                ClearScopeTag();
                break;
        }
    }

    [RelayCommand]
    private void ClearScopeTag()
    {
        SetScopeInternal("", projects: true, features: true, tasks: true, documents: true, ideas: true);
    }

    private void SetScopeWithTag(
        string moduleTitle,
        bool projects,
        bool features,
        bool tasks,
        bool documents,
        bool ideas)
    {
        var label = string.IsNullOrWhiteSpace(moduleTitle) ? "" : $"当前：{moduleTitle}";
        SetScopeInternal(label, projects, features, tasks, documents, ideas);
    }

    private void SetScopeInternal(
        string scopeTag,
        bool projects,
        bool features,
        bool tasks,
        bool documents,
        bool ideas)
    {
        _suppressScopeChanged = true;
        try
        {
            ScopeProjects = projects;
            ScopeFeatures = features;
            ScopeTasks = tasks;
            ScopeDocuments = documents;
            ScopeIdeas = ideas;
            ScopeTagText = scopeTag;
        }
        finally
        {
            _suppressScopeChanged = false;
        }

        ScheduleSearchImmediate();
    }

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
                var gvm = new GlobalSearchGroupViewModel(g.Key, sorted, KeywordForHighlight);
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

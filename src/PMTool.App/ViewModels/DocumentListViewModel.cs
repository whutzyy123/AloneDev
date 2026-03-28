using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using PMTool.App.Models;
using PMTool.App.Services;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;
using PMTool.Core.Validation;

namespace PMTool.App.ViewModels;

public enum DocumentSortField
{
    UpdatedAt,
    Name,
}

public partial class DocumentListViewModel(
    IDocumentRepository documentRepository,
    IProjectRepository projectRepository,
    IFeatureRepository featureRepository,
    ICurrentAccountContext accountContext,
    IDocumentImageStorage imageStorage) : ObservableObject, IOperationBarViewModel
{
    public const long MaxPastedImageBytes = 50L * 1024 * 1024;

    private readonly List<PmDocument> _documents = [];
    private readonly Dictionary<string, string> _projectNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _featureNames = new(StringComparer.Ordinal);

    private DispatcherQueueTimer? _searchHighlightClearTimer;
    private DispatcherQueueTimer? _searchDebounceTimer;
    private DispatcherQueueTimer? _autosaveTimer;
    private DispatcherQueueTimer? _debouncedAutosaveTimer;

    /// <summary>在从数据库回填编辑器时抑制防抖保存调度。</summary>
    private bool _suppressDebouncedAutosave;

    private string? _loadedDocId;
    private long _loadedRowVersion;
    private string _persistedContent = string.Empty;
    private string _persistedName = string.Empty;
    private bool _persistedSnippet;

    /// <summary>避免回退选中项时再次进入变更逻辑。</summary>
    private bool _suppressSelectionSync;

    private ReadOnlyObservableCollection<OperationBarMenuItem>? _filterMenuItems;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _sortMenuItems;

    public ObservableCollection<DocumentListRowViewModel> ListRows { get; } = [];

    [ObservableProperty]
    private DocumentListRowViewModel? _selectedListRow;

    [ObservableProperty]
    private string _markdownBody = string.Empty;

    [ObservableProperty]
    private string _editorName = string.Empty;

    [ObservableProperty]
    private bool _isCodeSnippetEditor;

    [ObservableProperty]
    private string _errorBanner = string.Empty;

    [ObservableProperty]
    private string? _relateTypeFilter;

    [ObservableProperty]
    private DocumentSortField _sortField = DocumentSortField.UpdatedAt;

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    private string _searchQuery = "";

    public string SearchPlaceholderText => "搜索文档名称或正文…";

    public bool IsOperationBarInteractive => true;

    public string PrimaryActionLabel => "新建文档";

    public IRelayCommand? PrimaryActionCommand => RequestNewDocumentUiCommand;

    public ReadOnlyObservableCollection<OperationBarMenuItem> FilterMenuItems =>
        _filterMenuItems ??= BuildFilterMenuItems();

    public ReadOnlyObservableCollection<OperationBarMenuItem> SortMenuItems =>
        _sortMenuItems ??= BuildSortMenuItems();

    public ObservableCollection<ProjectPickerItem> DialogProjectsMutable { get; } = [];

    public Visibility ErrorBannerVisibility =>
        string.IsNullOrEmpty(ErrorBanner) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EditorPanelVisibility =>
        _loadedDocId is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    public bool HasUnsavedChanges =>
        _loadedDocId is { Length: > 0 }
        && (MarkdownBody != _persistedContent
            || EditorName != _persistedName
            || IsCodeSnippetEditor != _persistedSnippet);

    public string DocumentSaveStatusText =>
        _loadedDocId is not { Length: > 0 }
            ? string.Empty
            : HasUnsavedChanges
                ? "将自动保存"
                : "已保存";

    public string FilterButtonText => RelateTypeFilter switch
    {
        null => "筛选：全部",
        DocumentRelateTypes.Global => "筛选：全局文档",
        DocumentRelateTypes.Project => "筛选：项目",
        DocumentRelateTypes.Feature => "筛选：模块",
        _ => "筛选",
    };

    public string SortButtonText => SortField switch
    {
        DocumentSortField.Name => SortDescending ? "排序：名称 · 降序" : "排序：名称 · 升序",
        _ => SortDescending ? "排序：更新时间 · 降序" : "排序：更新时间 · 升序",
    };

    public string? CurrentLoadedDocumentId => _loadedDocId;

    public event EventHandler? NewDocumentUiRequested;

    public event EventHandler? ExportHtmlUiRequested;

    public event EventHandler<int>? CaretMoveRequested;

    partial void OnErrorBannerChanged(string value) => OnPropertyChanged(nameof(ErrorBannerVisibility));

    partial void OnSelectedListRowChanged(DocumentListRowViewModel? value)
    {
        _ = OnSelectedListRowChangedAsync(value);
    }

    partial void OnMarkdownBodyChanged(string value)
    {
        NotifyEditorDirtyStateChanged();
        ScheduleDebouncedAutosave();
    }

    partial void OnEditorNameChanged(string value)
    {
        NotifyEditorDirtyStateChanged();
        ScheduleDebouncedAutosave();
    }

    partial void OnIsCodeSnippetEditorChanged(bool value)
    {
        NotifyEditorDirtyStateChanged();
        ScheduleDebouncedAutosave();
    }

    partial void OnRelateTypeFilterChanged(string? value)
    {
        OnPropertyChanged(nameof(FilterButtonText));
        RebuildListRows();
    }

    partial void OnSortFieldChanged(DocumentSortField value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        RebuildListRows();
    }

    partial void OnSortDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        RebuildListRows();
    }

    partial void OnSearchQueryChanged(string value) => ScheduleSearchRefresh();

    private async Task OnSelectedListRowChangedAsync(DocumentListRowViewModel? value)
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        if (value is { IsSectionHeader: true } or { Document: null })
        {
            return;
        }

        if (value is null)
        {
            if (_loadedDocId is not null && HasUnsavedChanges)
            {
                try
                {
                    ErrorBanner = "";
                    await PersistCurrentInternalAsync().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    ErrorBanner = ex.Message;
                    RevertSelectionToLoaded();
                    return;
                }
            }

            ClearEditor();
            return;
        }

        var doc = value.Document!;
        if (doc.Id == _loadedDocId)
        {
            return;
        }

        if (_loadedDocId is not null && HasUnsavedChanges)
        {
            try
            {
                ErrorBanner = "";
                await PersistCurrentInternalAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ErrorBanner = ex.Message;
                RevertSelectionToLoaded();
                return;
            }
        }

        await LoadEditorForDocumentAsync(doc).ConfigureAwait(true);
    }

    private void RevertSelectionToLoaded()
    {
        var revertRow = ListRows.FirstOrDefault(r => r.Document?.Id == _loadedDocId);
        if (revertRow is null)
        {
            return;
        }

        _suppressSelectionSync = true;
        SelectedListRow = revertRow;
        _suppressSelectionSync = false;
    }

    public void InsertImageMarkdownAt(int startIndex, string relativePath)
    {
        var fragment = $"![]({relativePath})";
        var text = MarkdownBody ?? string.Empty;
        var i = Math.Clamp(startIndex, 0, text.Length);
        MarkdownBody = text.Insert(i, fragment);
        CaretMoveRequested?.Invoke(this, i + fragment.Length);
        NotifyEditorDirtyStateChanged();
        ScheduleDebouncedAutosave();
    }

    public async Task HandlePasteImageAsync(int selectionStart, CancellationToken cancellationToken = default)
    {
        if (_loadedDocId is null)
        {
            ErrorBanner = "请先选择一篇文档后再粘贴图片。";
            return;
        }

        ErrorBanner = "";
        var data = Clipboard.GetContent();
        if (!data.Contains(StandardDataFormats.Bitmap))
        {
            return;
        }

        try
        {
            var bmpRef = await data.GetBitmapAsync().AsTask(cancellationToken).ConfigureAwait(true);
            if (bmpRef is null)
            {
                return;
            }

            using var ras = await bmpRef.OpenReadAsync().AsTask(cancellationToken).ConfigureAwait(true);
            using var managed = ras.AsStreamForRead();
            using var ms = new MemoryStream();
            await managed.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            var bytes = ms.ToArray();
            var rel = await imageStorage
                .SaveForDocumentAsync(_loadedDocId, bytes, ".png", MaxPastedImageBytes, cancellationToken)
                .ConfigureAwait(true);
            InsertImageMarkdownAt(selectionStart, rel);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        ErrorBanner = "";
        try
        {
            var pq = new ProjectListQuery(null, null, ProjectSortField.Name, false);
            var plist = await projectRepository.ListAsync(pq).ConfigureAwait(true);
            DialogProjectsMutable.Clear();
            _projectNames.Clear();
            foreach (var item in plist)
            {
                var id = item.Project.Id;
                _projectNames[id] = item.Project.Name;
                DialogProjectsMutable.Add(new ProjectPickerItem { Id = id, Name = item.Project.Name });
            }

            var docs = await documentRepository.ListActiveAsync().ConfigureAwait(true);
            _documents.Clear();
            _documents.AddRange(docs);
            _featureNames.Clear();
            var featureIds = docs
                .Where(d => d.RelateType == DocumentRelateTypes.Feature && d.FeatureId is { Length: > 0 } fid)
                .Select(d => d.FeatureId!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            foreach (var fid in featureIds)
            {
                var f = await featureRepository.GetByIdAsync(fid).ConfigureAwait(true);
                _featureNames[fid] = f?.Name ?? fid;
            }

            RebuildListRows(selectDocumentId: _loadedDocId ?? SelectedListRow?.Document?.Id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private void RebuildListRows(string? selectDocumentId = null)
    {
        var selId = selectDocumentId ?? SelectedListRow?.Document?.Id;
        IEnumerable<PmDocument> Filtered(IEnumerable<PmDocument> q)
        {
            foreach (var d in q)
            {
                if (RelateTypeFilter is { Length: > 0 } rt && d.RelateType != rt)
                {
                    continue;
                }

                if (!MatchesSearch(d))
                {
                    continue;
                }

                yield return d;
            }
        }

        IEnumerable<PmDocument> Order(IEnumerable<PmDocument> items)
        {
            return SortField == DocumentSortField.Name
                ? SortDescending
                    ? items.OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    : items.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                : SortDescending
                    ? items.OrderByDescending(d => d.UpdatedAt, StringComparer.Ordinal)
                    : items.OrderBy(d => d.UpdatedAt, StringComparer.Ordinal);
        }

        ListRows.Clear();
        var globals = Order(Filtered(_documents.Where(d => d.RelateType == DocumentRelateTypes.Global))).ToList();
        if (globals.Count > 0)
        {
            ListRows.Add(new DocumentListRowViewModel { IsSectionHeader = true, SectionTitle = "全局文档" });
            foreach (var d in globals)
            {
                ListRows.Add(new DocumentListRowViewModel
                {
                    Document = d,
                    Subtitle = string.Empty,
                });
            }
        }

        var projects = Order(Filtered(_documents.Where(d => d.RelateType == DocumentRelateTypes.Project))).ToList();
        if (projects.Count > 0)
        {
            ListRows.Add(new DocumentListRowViewModel { IsSectionHeader = true, SectionTitle = "项目文档" });
            foreach (var d in projects)
            {
                var sub = d.ProjectId is { Length: > 0 } pid && _projectNames.TryGetValue(pid, out var pn)
                    ? pn
                    : string.Empty;
                ListRows.Add(new DocumentListRowViewModel { Document = d, Subtitle = sub });
            }
        }

        var feats = Order(Filtered(_documents.Where(d => d.RelateType == DocumentRelateTypes.Feature))).ToList();
        if (feats.Count > 0)
        {
            ListRows.Add(new DocumentListRowViewModel { IsSectionHeader = true, SectionTitle = "模块文档" });
            foreach (var d in feats)
            {
                var fn = d.FeatureId is { Length: > 0 } x && _featureNames.TryGetValue(x, out var v) ? v : "模块";
                var pn = d.ProjectId is { Length: > 0 } p && _projectNames.TryGetValue(p, out var pv) ? pv : "项目";
                ListRows.Add(new DocumentListRowViewModel { Document = d, Subtitle = $"{pn} · {fn}" });
            }
        }

        DocumentListRowViewModel? nextSel = null;
        if (selId is { Length: > 0 })
        {
            nextSel = ListRows.FirstOrDefault(r => r.Document?.Id == selId);
        }

        SelectedListRow = nextSel;
        OnPropertyChanged(nameof(EditorPanelVisibility));
    }

    private bool MatchesSearch(PmDocument d)
    {
        var q = SearchQuery.Trim();
        if (q.Length == 0)
        {
            return true;
        }

        return d.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
               || d.Content.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadEditorForDocumentAsync(PmDocument doc)
    {
        var fresh = await documentRepository.GetByIdAsync(doc.Id).ConfigureAwait(true);
        if (fresh is null)
        {
            ErrorBanner = "文档不存在或已删除。";
            ClearEditor();
            return;
        }

        _suppressDebouncedAutosave = true;
        try
        {
            _loadedDocId = fresh.Id;
            _loadedRowVersion = fresh.RowVersion;
            MarkdownBody = fresh.Content;
            EditorName = fresh.Name;
            IsCodeSnippetEditor = fresh.IsCodeSnippet;
            _persistedContent = fresh.Content;
            _persistedName = fresh.Name;
            _persistedSnippet = fresh.IsCodeSnippet;
        }
        finally
        {
            _suppressDebouncedAutosave = false;
        }

        NotifyEditorDirtyStateChanged();
        OnPropertyChanged(nameof(EditorPanelVisibility));
    }

    private void ClearEditor()
    {
        _suppressDebouncedAutosave = true;
        try
        {
            _loadedDocId = null;
            _loadedRowVersion = 0;
            MarkdownBody = string.Empty;
            EditorName = string.Empty;
            IsCodeSnippetEditor = false;
            _persistedContent = string.Empty;
            _persistedName = string.Empty;
            _persistedSnippet = false;
        }
        finally
        {
            _suppressDebouncedAutosave = false;
        }

        NotifyEditorDirtyStateChanged();
        OnPropertyChanged(nameof(EditorPanelVisibility));
    }

    private void ScheduleSearchRefresh()
    {
        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is null)
        {
            return;
        }

        _searchDebounceTimer ??= dq.CreateTimer();
        _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(500);
        _searchDebounceTimer.IsRepeating = false;
        _searchDebounceTimer.Tick -= OnSearchDebounceTick;
        _searchDebounceTimer.Tick += OnSearchDebounceTick;
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void OnSearchDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnSearchDebounceTick;
        RebuildListRows(selectDocumentId: _loadedDocId);
    }

    [RelayCommand]
    private void RequestNewDocumentUi() => NewDocumentUiRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task SaveCurrentAsync()
    {
        if (_loadedDocId is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            await PersistCurrentInternalAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private async Task PersistCurrentInternalAsync(CancellationToken cancellationToken = default)
    {
        if (_loadedDocId is null)
        {
            return;
        }

        _suppressDebouncedAutosave = true;
        try
        {
            var name = DocumentFieldValidator.ValidateName(EditorName);
            var content = DocumentFieldValidator.ValidateContent(MarkdownBody);
            var fmt = DocumentFieldValidator.ValidateContentFormat(DocumentContentFormats.Markdown);
            await documentRepository
                .UpdateFullAsync(_loadedDocId, name, IsCodeSnippetEditor, content, fmt, _loadedRowVersion, snippetLanguage: null, cancellationToken)
                .ConfigureAwait(true);
            var updated = await documentRepository.GetByIdAsync(_loadedDocId, cancellationToken).ConfigureAwait(true);
            if (updated is null)
            {
                throw new InvalidOperationException("保存后无法重新加载文档。");
            }

            _loadedRowVersion = updated.RowVersion;
            _persistedContent = updated.Content;
            _persistedName = updated.Name;
            _persistedSnippet = updated.IsCodeSnippet;
            MarkdownBody = updated.Content;
            EditorName = updated.Name;
            IsCodeSnippetEditor = updated.IsCodeSnippet;
            var idx = _documents.FindIndex(d => d.Id == updated.Id);
            if (idx >= 0)
            {
                _documents[idx] = updated;
            }

            RebuildListRows(selectDocumentId: updated.Id);
        }
        finally
        {
            _suppressDebouncedAutosave = false;
        }

        NotifyEditorDirtyStateChanged();
    }

    [RelayCommand]
    private void RequestExportHtmlUi()
    {
        if (_loadedDocId is null)
        {
            ErrorBanner = "请先选择要导出的文档。";
            return;
        }

        ErrorBanner = "";
        ExportHtmlUiRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task ExportHtmlToPathAsync(string htmlPath, CancellationToken cancellationToken = default)
    {
        if (_loadedDocId is null)
        {
            return;
        }

        var title = DocumentFieldValidator.ValidateName(EditorName);
        var md = MarkdownBody;
        var root = accountContext.GetAccountDirectoryPath();
        await DocumentHtmlExporter
            .ExportMarkdownToHtmlFileAsync(root, title, md, htmlPath, cancellationToken)
            .ConfigureAwait(true);
    }

    public async Task CreateDocumentAsync(string relateType, string? projectId, string? featureId, string name, CancellationToken cancellationToken = default)
    {
        var n = DocumentFieldValidator.ValidateName(name);
        DocumentFieldValidator.ValidateRelation(relateType, projectId, featureId);
        var now = Now();
        var doc = new PmDocument
        {
            Id = Guid.NewGuid().ToString("D"),
            ProjectId = projectId,
            FeatureId = featureId,
            Name = n,
            RelateType = relateType,
            Content = string.Empty,
            ContentFormat = DocumentContentFormats.Markdown,
            IsCodeSnippet = false,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            RowVersion = 1,
        };
        await documentRepository.InsertAsync(doc, cancellationToken).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        var row = ListRows.FirstOrDefault(r => r.Document?.Id == doc.Id);
        if (row is not null)
        {
            SelectedListRow = row;
        }
    }

    public async Task RenameLoadedDocumentAsync(string newName, CancellationToken cancellationToken = default)
    {
        if (_loadedDocId is null)
        {
            return;
        }

        EditorName = DocumentFieldValidator.ValidateName(newName);
        await PersistCurrentInternalAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task DeleteLoadedDocumentAsync(CancellationToken cancellationToken = default)
    {
        if (_loadedDocId is null)
        {
            return;
        }

        await documentRepository.SoftDeleteAsync(_loadedDocId, _loadedRowVersion, cancellationToken).ConfigureAwait(true);
        ClearEditor();
        SelectedListRow = null;
        await RefreshAsync().ConfigureAwait(true);
    }

    public Task<IReadOnlyList<Feature>> LoadFeaturesForProjectAsync(
        string projectId,
        CancellationToken cancellationToken = default) =>
        featureRepository.ListAsync(
            new FeatureListQuery
            {
                ProjectId = projectId,
                SortField = FeatureSortField.Name,
                SortDescending = false,
            },
            cancellationToken);

    public void EnsureAutosaveTimer()
    {
        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is null)
        {
            return;
        }

        if (_autosaveTimer is not null)
        {
            return;
        }

        _autosaveTimer = dq.CreateTimer();
        _autosaveTimer.Interval = TimeSpan.FromMinutes(3);
        _autosaveTimer.IsRepeating = true;
        _autosaveTimer.Tick += async (_, _) =>
        {
            try
            {
                if (_loadedDocId is not null && HasUnsavedChanges)
                {
                    await PersistCurrentInternalAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                ErrorBanner = $"自动保存失败：{ex.Message}";
            }
        };
        _autosaveTimer.Start();
    }

    private void NotifyEditorDirtyStateChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(DocumentSaveStatusText));
    }

    private void ScheduleDebouncedAutosave()
    {
        if (_suppressDebouncedAutosave)
        {
            return;
        }

        if (_loadedDocId is null || !HasUnsavedChanges)
        {
            return;
        }

        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is null)
        {
            return;
        }

        _debouncedAutosaveTimer ??= dq.CreateTimer();
        _debouncedAutosaveTimer.Interval = TimeSpan.FromSeconds(2);
        _debouncedAutosaveTimer.IsRepeating = false;
        _debouncedAutosaveTimer.Tick -= OnDebouncedAutosaveTick;
        _debouncedAutosaveTimer.Tick += OnDebouncedAutosaveTick;
        _debouncedAutosaveTimer.Stop();
        _debouncedAutosaveTimer.Start();
    }

    private async void OnDebouncedAutosaveTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnDebouncedAutosaveTick;
        try
        {
            if (_loadedDocId is not null && HasUnsavedChanges && !_suppressDebouncedAutosave)
            {
                ErrorBanner = "";
                await PersistCurrentInternalAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            ErrorBanner = $"自动保存失败：{ex.Message}";
        }
    }

    public async Task FlushPendingDebouncedAutosaveAsync()
    {
        if (_debouncedAutosaveTimer is not null)
        {
            _debouncedAutosaveTimer.Tick -= OnDebouncedAutosaveTick;
            _debouncedAutosaveTimer.Stop();
        }

        if (_loadedDocId is null || !HasUnsavedChanges)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            await PersistCurrentInternalAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    public void ReleaseDebouncedAutosaveTimer()
    {
        if (_debouncedAutosaveTimer is null)
        {
            return;
        }

        _debouncedAutosaveTimer.Tick -= OnDebouncedAutosaveTick;
        _debouncedAutosaveTimer.Stop();
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildFilterMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "全部", Command = SetFilterAllCommand },
            new() { Text = "全局文档", Command = SetFilterGlobalCommand },
            new() { Text = "项目", Command = SetFilterProjectCommand },
            new() { Text = "模块", Command = SetFilterFeatureCommand },
        };
        return new ReadOnlyObservableCollection<OperationBarMenuItem>(items);
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildSortMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "更新时间 · 降序", Command = SetSortUpdatedDescCommand },
            new() { Text = "更新时间 · 升序", Command = SetSortUpdatedAscCommand },
            new() { Text = "名称 · 升序", Command = SetSortNameAscCommand },
            new() { Text = "名称 · 降序", Command = SetSortNameDescCommand },
        };
        return new ReadOnlyObservableCollection<OperationBarMenuItem>(items);
    }

    [RelayCommand]
    private void SetFilterAll() => RelateTypeFilter = null;

    [RelayCommand]
    private void SetFilterGlobal() => RelateTypeFilter = DocumentRelateTypes.Global;

    [RelayCommand]
    private void SetFilterProject() => RelateTypeFilter = DocumentRelateTypes.Project;

    [RelayCommand]
    private void SetFilterFeature() => RelateTypeFilter = DocumentRelateTypes.Feature;

    [RelayCommand]
    private void SetSortUpdatedDesc()
    {
        SortField = DocumentSortField.UpdatedAt;
        SortDescending = true;
    }

    [RelayCommand]
    private void SetSortUpdatedAsc()
    {
        SortField = DocumentSortField.UpdatedAt;
        SortDescending = false;
    }

    [RelayCommand]
    private void SetSortNameAsc()
    {
        SortField = DocumentSortField.Name;
        SortDescending = false;
    }

    [RelayCommand]
    private void SetSortNameDesc()
    {
        SortField = DocumentSortField.Name;
        SortDescending = true;
    }

    public async Task JumpToEntityFromSearchAsync(string documentId)
    {
        RelateTypeFilter = null;
        SearchQuery = "";
        ClearDocumentSearchHighlights();
        await RefreshAsync().ConfigureAwait(true);
        var row = ListRows.FirstOrDefault(r => r.Document?.Id == documentId && !r.IsSectionHeader);
        if (row is null)
        {
            return;
        }

        row.IsSearchHighlight = true;
        SelectedListRow = row;
        ScheduleDocumentSearchHighlightClear();
    }

    private void ClearDocumentSearchHighlights()
    {
        foreach (var r in ListRows)
        {
            if (!r.IsSectionHeader)
            {
                r.IsSearchHighlight = false;
            }
        }
    }

    private void ScheduleDocumentSearchHighlightClear()
    {
        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is null)
        {
            return;
        }

        _searchHighlightClearTimer?.Stop();
        _searchHighlightClearTimer = dq.CreateTimer();
        _searchHighlightClearTimer.Interval = TimeSpan.FromSeconds(3);
        _searchHighlightClearTimer.IsRepeating = false;
        _searchHighlightClearTimer.Tick += OnDocumentSearchHighlightClearTick;
        _searchHighlightClearTimer.Start();
    }

    private void OnDocumentSearchHighlightClearTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnDocumentSearchHighlightClearTick;
        ClearDocumentSearchHighlights();
    }

    private static string Now() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}

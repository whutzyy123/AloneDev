using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PMTool.App.Models;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;
using PMTool.Core.Validation;

namespace PMTool.App.ViewModels;

public partial class SnippetListViewModel(
    IDocumentRepository documentRepository,
    IProjectRepository projectRepository) : ObservableObject, IOperationBarViewModel
{
    private DispatcherQueueTimer? _searchDebounceTimer;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _listScopeMenuItems;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _sortMenuItems;
    private bool _suppressSelectionReload;
    private string? _editingId;
    private long _editingRowVersion;
    private bool _isNewDraft;

    public ObservableCollection<PmDocument> Snippets { get; } = [];

    public ObservableCollection<ProjectPickerItem> ProjectOptions { get; } = [];

    public IReadOnlyList<string> LanguageOptions => DocumentFieldValidator.SnippetLanguagePickerOptions;

    public string SearchPlaceholderText => "搜索片段名称或源码…";

    public bool IsOperationBarInteractive => true;

    public bool ShowModuleSearch => true;

    public string PrimaryActionLabel => "新建片段";

    public IRelayCommand? PrimaryActionCommand => BeginNewDraftCommand;

    public ReadOnlyObservableCollection<OperationBarMenuItem> FilterMenuItems =>
        _listScopeMenuItems ??= BuildListScopeMenuItems();

    public ReadOnlyObservableCollection<OperationBarMenuItem> SortMenuItems =>
        _sortMenuItems ??= BuildSortMenuItems();

    [ObservableProperty]
    private PmDocument? _selectedSnippet;

    [ObservableProperty]
    private CodeSnippetListScope _listScope = CodeSnippetListScope.All;

    [ObservableProperty]
    private string? _listFilterProjectId;

    [ObservableProperty]
    private CodeSnippetSortField _sortField = CodeSnippetSortField.UpdatedAt;

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _errorBanner = "";

    [ObservableProperty]
    private string _editorName = "";

    [ObservableProperty]
    private string _editorRelateType = DocumentRelateTypes.Global;

    [ObservableProperty]
    private string? _editorProjectId;

    [ObservableProperty]
    private string _editorLanguage = "plaintext";

    [ObservableProperty]
    private string _sourceText = "";

    public string FilterButtonText => ListScope switch
    {
        CodeSnippetListScope.All => "范围：全部",
        CodeSnippetListScope.GlobalOnly => "范围：仅全局",
        CodeSnippetListScope.ByProject => "范围：项目+全局",
        _ => "范围",
    };

    public string SortButtonText =>
        $"{(SortField == CodeSnippetSortField.Name ? "名称" : "更新时间")} · {(SortDescending ? "降序" : "升序")}";

    public Visibility ErrorBannerVisibility =>
        string.IsNullOrEmpty(ErrorBanner) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EditorPanelVisibility =>
        SelectedSnippet is not null || _isNewDraft ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyHintVisibility =>
        SelectedSnippet is null && !_isNewDraft ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ListFilterProjectVisibility =>
        ListScope == CodeSnippetListScope.ByProject ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EditorProjectVisibility =>
        EditorRelateType == DocumentRelateTypes.Project ? Visibility.Visible : Visibility.Collapsed;

    public event EventHandler? SnippetPreviewInvalidated;

    partial void OnSelectedSnippetChanged(PmDocument? value)
    {
        if (_suppressSelectionReload)
        {
            return;
        }

        if (value is null)
        {
            if (!_isNewDraft)
            {
                ClearEditor();
            }
            else
            {
                NotifyEditorChrome();
            }

            return;
        }

        _isNewDraft = false;
        _editingId = value.Id;
        _editingRowVersion = value.RowVersion;
        EditorName = value.Name;
        EditorRelateType = value.RelateType;
        EditorProjectId = value.ProjectId;
        EditorLanguage = string.IsNullOrWhiteSpace(value.SnippetLanguage) ? "plaintext" : value.SnippetLanguage;
        SourceText = value.Content;
        RaisePreviewInvalidated();
        NotifyEditorChrome();
    }

    partial void OnSourceTextChanged(string value) => RaisePreviewInvalidated();

    partial void OnEditorLanguageChanged(string value) => RaisePreviewInvalidated();

    partial void OnListScopeChanged(CodeSnippetListScope value)
    {
        OnPropertyChanged(nameof(FilterButtonText));
        OnPropertyChanged(nameof(ListFilterProjectVisibility));
        _ = ReloadListAsync();
    }

    partial void OnListFilterProjectIdChanged(string? value) => _ = ReloadListAsync();

    partial void OnSortFieldChanged(CodeSnippetSortField value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = ReloadListAsync();
    }

    partial void OnSortDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = ReloadListAsync();
    }

    partial void OnSearchQueryChanged(string value) => ScheduleSearchReload();

    partial void OnEditorRelateTypeChanged(string value)
    {
        if (value == DocumentRelateTypes.Global)
        {
            EditorProjectId = null;
        }

        OnPropertyChanged(nameof(EditorProjectVisibility));
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ErrorBanner = "";
        try
            {
            await LoadProjectsAsync(cancellationToken).ConfigureAwait(true);
            await ReloadListAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private async Task LoadProjectsAsync(CancellationToken cancellationToken)
    {
        var rows = await projectRepository
            .ListAsync(new ProjectListQuery(null, null, ProjectSortField.Name, false), cancellationToken)
            .ConfigureAwait(false);
        ProjectOptions.Clear();
        foreach (var row in rows)
        {
            ProjectOptions.Add(new ProjectPickerItem { Id = row.Project.Id, Name = row.Project.Name });
        }
    }

    private void ScheduleSearchReload()
    {
        var dq = DispatcherQueue.GetForCurrentThread();
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
        _ = ReloadListAsync();
    }

    private async Task ReloadListAsync(CancellationToken cancellationToken = default)
    {
        if (ListScope == CodeSnippetListScope.ByProject && string.IsNullOrWhiteSpace(ListFilterProjectId))
        {
            Snippets.Clear();
            ErrorBanner = "";
            return;
        }

        try
        {
            var q = new CodeSnippetListQuery
            {
                Scope = ListScope,
                ProjectFilterId = ListScope == CodeSnippetListScope.ByProject ? ListFilterProjectId : null,
                SearchText = SearchQuery,
                SortField = SortField,
                SortDescending = SortDescending,
            };
            var list = await documentRepository.ListCodeSnippetsAsync(q, cancellationToken).ConfigureAwait(true);
            _suppressSelectionReload = true;
            try
            {
                Snippets.Clear();
                foreach (var d in list)
                {
                    Snippets.Add(d);
                }

                if (_editingId is not null)
                {
                    var match = Snippets.FirstOrDefault(s => s.Id == _editingId);
                    if (match is not null)
                    {
                        SelectedSnippet = match;
                    }
                }
            }
            finally
            {
                _suppressSelectionReload = false;
            }

            ErrorBanner = "";
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private void BeginNewDraft()
    {
        ErrorBanner = "";
        _isNewDraft = true;
        _editingId = Guid.NewGuid().ToString("N");
        _editingRowVersion = 1;
        _suppressSelectionReload = true;
        SelectedSnippet = null;
        _suppressSelectionReload = false;
        EditorName = "未命名片段";
        EditorRelateType = DocumentRelateTypes.Global;
        EditorProjectId = null;
        EditorLanguage = "plaintext";
        SourceText = "";
        NotifyEditorChrome();
        RaisePreviewInvalidated();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorBanner = "";
        try
        {
            var name = DocumentFieldValidator.ValidateName(EditorName);
            var body = DocumentFieldValidator.ValidateContent(SourceText);
            var fmt = DocumentFieldValidator.ValidateContentFormat(DocumentContentFormats.PlainText);
            var relate = EditorRelateType is DocumentRelateTypes.Global or DocumentRelateTypes.Project
                ? EditorRelateType
                : DocumentRelateTypes.Global;
            var pid = relate == DocumentRelateTypes.Project ? EditorProjectId : null;
            DocumentFieldValidator.ValidateRelation(relate, pid, null);

            if (_isNewDraft)
            {
                var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                var doc = new PmDocument
                {
                    Id = _editingId!,
                    ProjectId = pid,
                    FeatureId = null,
                    Name = name,
                    RelateType = relate,
                    Content = body,
                    ContentFormat = fmt,
                    IsCodeSnippet = true,
                    SnippetLanguage = EditorLanguage,
                    CreatedAt = stamp,
                    UpdatedAt = stamp,
                    IsDeleted = false,
                    RowVersion = 1,
                };
                await documentRepository.InsertAsync(doc).ConfigureAwait(true);
                _isNewDraft = false;
            }
            else if (_editingId is not null)
            {
                var lang = DocumentFieldValidator.NormalizeSnippetLanguageForStorage(EditorLanguage, true);
                await documentRepository
                    .UpdateFullAsync(
                        _editingId,
                        name,
                        isCodeSnippet: true,
                        body,
                        fmt,
                        _editingRowVersion,
                        lang,
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }

            await ReloadListAsync().ConfigureAwait(true);
            var latest = Snippets.FirstOrDefault(s => s.Id == _editingId);
            if (latest is not null)
            {
                _suppressSelectionReload = true;
                SelectedSnippet = latest;
                _suppressSelectionReload = false;
                _editingRowVersion = latest.RowVersion;
            }

            RaisePreviewInvalidated();
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private void CopySource()
    {
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(SourceText ?? string.Empty);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
    }

    private void ClearEditor()
    {
        _isNewDraft = false;
        _editingId = null;
        _editingRowVersion = 0;
        EditorName = "";
        EditorRelateType = DocumentRelateTypes.Global;
        EditorProjectId = null;
        EditorLanguage = "plaintext";
        SourceText = "";
        NotifyEditorChrome();
    }

    private void NotifyEditorChrome()
    {
        OnPropertyChanged(nameof(EditorPanelVisibility));
        OnPropertyChanged(nameof(EmptyHintVisibility));
    }

    private void RaisePreviewInvalidated() =>
        SnippetPreviewInvalidated?.Invoke(this, EventArgs.Empty);

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildListScopeMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "全部片段", Command = SetScopeAllCommand },
            new() { Text = "仅全局片段", Command = SetScopeGlobalCommand },
            new() { Text = "指定项目（含全局）", Command = SetScopeProjectCommand },
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
    private void SetScopeAll()
    {
        ListScope = CodeSnippetListScope.All;
    }

    [RelayCommand]
    private void SetScopeGlobal()
    {
        ListScope = CodeSnippetListScope.GlobalOnly;
    }

    [RelayCommand]
    private void SetScopeProject()
    {
        ListScope = CodeSnippetListScope.ByProject;
    }

    [RelayCommand]
    private void SetSortUpdatedDesc()
    {
        SortField = CodeSnippetSortField.UpdatedAt;
        SortDescending = true;
    }

    [RelayCommand]
    private void SetSortUpdatedAsc()
    {
        SortField = CodeSnippetSortField.UpdatedAt;
        SortDescending = false;
    }

    [RelayCommand]
    private void SetSortNameAsc()
    {
        SortField = CodeSnippetSortField.Name;
        SortDescending = false;
    }

    [RelayCommand]
    private void SetSortNameDesc()
    {
        SortField = CodeSnippetSortField.Name;
        SortDescending = true;
    }
}

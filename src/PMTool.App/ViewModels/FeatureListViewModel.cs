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

public partial class FeatureListViewModel(
    IFeatureRepository featureRepository,
    IProjectRepository projectRepository,
    IFeatureDeletionGuard featureDeletionGuard,
    ITaskRepository taskRepository) : ObservableObject, IOperationBarViewModel
{
    private bool _suppressProjectRefreshForSearchJump;
    private DispatcherQueueTimer? _searchHighlightClearTimer;
    private DispatcherQueueTimer? _searchDebounceTimer;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _filterMenuItems;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _sortMenuItems;

    public string SearchPlaceholderText => "搜索特性名称或描述…";

    public bool IsOperationBarInteractive => SelectedProjectId is { Length: > 0 };

    public string PrimaryActionLabel => "新建特性";

    public IRelayCommand? PrimaryActionCommand => RequestNewFeatureUiCommand;

    public ReadOnlyObservableCollection<OperationBarMenuItem> FilterMenuItems =>
        _filterMenuItems ??= BuildFilterMenuItems();

    public ReadOnlyObservableCollection<OperationBarMenuItem> SortMenuItems =>
        _sortMenuItems ??= BuildSortMenuItems();

    public ObservableCollection<ProjectPickerItem> ProjectOptions { get; } = [];

    public ObservableCollection<FeatureRowViewModel> Features { get; } = [];

    public ObservableCollection<FeatureRowViewModel> KanbanFeaturesToPlan { get; } = [];

    public ObservableCollection<FeatureRowViewModel> KanbanFeaturesInProgress { get; } = [];

    public ObservableCollection<FeatureRowViewModel> KanbanFeaturesDone { get; } = [];

    public ObservableCollection<FeatureRowViewModel> KanbanFeaturesReleased { get; } = [];

    public ObservableCollection<string> DetailAllowedStatuses { get; } = [];

    [ObservableProperty]
    private string? _selectedProjectId;

    [ObservableProperty]
    private FeatureRowViewModel? _selectedFeature;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string? _statusFilter;

    [ObservableProperty]
    private FeatureSortField _sortField = FeatureSortField.UpdatedAt;

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    private string _errorBanner = "";

    [ObservableProperty]
    private bool _isKanbanView;

    [ObservableProperty]
    private string _detailStatusDraft = FeatureStatuses.ToPlan;

    [ObservableProperty]
    private string _detailTaskProgressText = string.Empty;

    public string FilterButtonText => StatusFilter switch
    {
        null => "筛选：全部",
        FeatureStatuses.ToPlan => "筛选：待规划",
        FeatureStatuses.InProgress => "筛选：进行中",
        FeatureStatuses.Done => "筛选：已完成",
        FeatureStatuses.Released => "筛选：已上线",
        _ => "筛选",
    };

    public string SortButtonText =>
        $"{SortFieldToLabel(SortField)} · {(SortDescending ? "降序" : "升序")}";

    public Visibility ErrorBannerVisibility =>
        string.IsNullOrEmpty(ErrorBanner) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DetailPanelVisibility =>
        SelectedFeature is null ? Visibility.Collapsed : Visibility.Visible;

    public bool ShowProjectsMissing => ProjectOptions.Count == 0;

    public bool ShowSelectProject => ProjectOptions.Count > 0 && string.IsNullOrEmpty(SelectedProjectId);

    public bool ShowGlobalFeatureEmpty =>
        !string.IsNullOrEmpty(SelectedProjectId) && Features.Count == 0
        && string.IsNullOrWhiteSpace(SearchQuery) && StatusFilter is null;

    public bool ShowNoMatch =>
        !string.IsNullOrEmpty(SelectedProjectId) && Features.Count == 0
        && (!string.IsNullOrWhiteSpace(SearchQuery) || StatusFilter is not null);

    public Visibility ListVisibility => Features.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TableHeaderVisibility => IsKanbanView ? Visibility.Collapsed : Visibility.Visible;

    public Visibility TableMainVisibility =>
        !IsKanbanView && ListVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility KanbanMainVisibility =>
        IsKanbanView && ListVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyProjectsVisibility => ShowProjectsMissing ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectProjectVisibility => ShowSelectProject ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GlobalEmptyVisibility => ShowGlobalFeatureEmpty ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoMatchVisibility => ShowNoMatch ? Visibility.Visible : Visibility.Collapsed;

    public string DetailTitle => SelectedFeature?.Name ?? string.Empty;

    public string DetailMeta =>
        SelectedFeature is { } f ? $"{f.PriorityLabel} · {f.Status} · 更新 {f.UpdatedAt}" : string.Empty;

    public event EventHandler? NewFeatureUiRequested;

    public event EventHandler? EditFeatureUiRequested;

    public Task<Feature?> GetFeatureEntityAsync(string id) => featureRepository.GetByIdAsync(id);

    partial void OnSelectedProjectIdChanged(string? value)
    {
        OnPropertyChanged(nameof(IsOperationBarInteractive));
        OnPropertyChanged(nameof(ShowSelectProject));
        OnPropertyChanged(nameof(SelectProjectVisibility));
        if (_suppressProjectRefreshForSearchJump)
        {
            return;
        }

        _ = RefreshFeaturesOnlyAsync();
    }

    partial void OnSelectedFeatureChanged(FeatureRowViewModel? value)
    {
        if (value is null)
        {
            DetailAllowedStatuses.Clear();
            DetailStatusDraft = FeatureStatuses.ToPlan;
            DetailTaskProgressText = string.Empty;
        }
        else
        {
            RebuildAllowedStatuses(value.Status);
            DetailStatusDraft = value.Status;
            _ = UpdateDetailTaskProgressAsync();
        }

        OnPropertyChanged(nameof(DetailPanelVisibility));
        OnPropertyChanged(nameof(DetailTitle));
        OnPropertyChanged(nameof(DetailMeta));
    }

    partial void OnErrorBannerChanged(string value) => OnPropertyChanged(nameof(ErrorBannerVisibility));

    partial void OnIsKanbanViewChanged(bool value)
    {
        OnPropertyChanged(nameof(TableHeaderVisibility));
        OnPropertyChanged(nameof(TableMainVisibility));
        OnPropertyChanged(nameof(KanbanMainVisibility));
    }

    partial void OnStatusFilterChanged(string? value)
    {
        OnPropertyChanged(nameof(FilterButtonText));
        _ = RefreshFeaturesOnlyAsync();
    }

    partial void OnSortFieldChanged(FeatureSortField value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = RefreshFeaturesOnlyAsync();
    }

    partial void OnSortDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = RefreshFeaturesOnlyAsync();
    }

    partial void OnSearchQueryChanged(string value) => ScheduleSearchRefresh();

    private async Task UpdateDetailTaskProgressAsync()
    {
        if (SelectedFeature is null)
        {
            DetailTaskProgressText = string.Empty;
            return;
        }

        try
        {
            var p = await taskRepository.GetFeatureProgressAsync(SelectedFeature.Id).ConfigureAwait(true);
            DetailTaskProgressText =
                $"{p.CompletedCount}/{p.TotalExcludingCancelled} · {p.Percent}%（分母不含已取消）";
        }
        catch
        {
            DetailTaskProgressText = "任务进度加载失败";
        }
    }

    private void RebuildAllowedStatuses(string currentStatus)
    {
        DetailAllowedStatuses.Clear();
        foreach (var s in FeatureStatusTransitions.GetAllowedTargets(currentStatus))
        {
            DetailAllowedStatuses.Add(s);
        }
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildFilterMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "全部", Command = SetFilterAllCommand },
            new() { Text = "待规划", Command = SetFilterToPlanCommand },
            new() { Text = "进行中", Command = SetFilterInProgressCommand },
            new() { Text = "已完成", Command = SetFilterDoneCommand },
            new() { Text = "已上线", Command = SetFilterReleasedCommand },
        };
        return new ReadOnlyObservableCollection<OperationBarMenuItem>(items);
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildSortMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "更新时间 · 降序", Command = SetSortUpdatedDescCommand },
            new() { Text = "名称 · 升序", Command = SetSortNameAscCommand },
            new() { Text = "创建时间 · 降序", Command = SetSortCreatedDescCommand },
        };
        return new ReadOnlyObservableCollection<OperationBarMenuItem>(items);
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

    private async void OnSearchDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnSearchDebounceTick;
        await RefreshFeaturesOnlyAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        ErrorBanner = "";
        try
        {
            var pq = new ProjectListQuery(null, null, ProjectSortField.UpdatedAt, true);
            var plist = await projectRepository.ListAsync(pq).ConfigureAwait(true);
            ProjectOptions.Clear();
            foreach (var item in plist)
            {
                ProjectOptions.Add(new ProjectPickerItem
                {
                    Id = item.Project.Id,
                    Name = item.Project.Name,
                });
            }

            if (ProjectOptions.Count > 0)
            {
                if (string.IsNullOrEmpty(SelectedProjectId)
                    || ProjectOptions.All(p => p.Id != SelectedProjectId))
                {
                    SelectedProjectId = ProjectOptions[0].Id;
                }
            }
            else
            {
                SelectedProjectId = null;
            }

            OnPropertyChanged(nameof(IsOperationBarInteractive));
            OnPropertyChanged(nameof(ShowProjectsMissing));
            OnPropertyChanged(nameof(ShowSelectProject));
            OnPropertyChanged(nameof(EmptyProjectsVisibility));
            OnPropertyChanged(nameof(SelectProjectVisibility));
            await RefreshFeaturesOnlyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private async Task RefreshFeaturesOnlyAsync()
    {
        ErrorBanner = "";
        try
        {
            var prevId = SelectedFeature?.Id;
            if (string.IsNullOrEmpty(SelectedProjectId))
            {
                Features.Clear();
                SelectedFeature = null;
                RebuildKanbanColumns();
            }
            else
            {
                var q = new FeatureListQuery
                {
                    ProjectId = SelectedProjectId,
                    SearchText = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim(),
                    StatusFilter = StatusFilter,
                    SortField = SortField,
                    SortDescending = SortDescending,
                };
                var list = await featureRepository.ListAsync(q).ConfigureAwait(true);
                Features.Clear();
                foreach (var f in list)
                {
                    Features.Add(FeatureRowViewModel.FromFeature(f));
                }

                if (prevId is not null)
                {
                    SelectedFeature = Features.FirstOrDefault(x => x.Id == prevId);
                }
            }

            OnPropertyChanged(nameof(ShowGlobalFeatureEmpty));
            OnPropertyChanged(nameof(ShowNoMatch));
            OnPropertyChanged(nameof(GlobalEmptyVisibility));
            OnPropertyChanged(nameof(NoMatchVisibility));
            OnPropertyChanged(nameof(ListVisibility));
            OnPropertyChanged(nameof(TableMainVisibility));
            OnPropertyChanged(nameof(KanbanMainVisibility));
            RebuildKanbanColumns();
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private void RebuildKanbanColumns()
    {
        KanbanFeaturesToPlan.Clear();
        KanbanFeaturesInProgress.Clear();
        KanbanFeaturesDone.Clear();
        KanbanFeaturesReleased.Clear();
        foreach (var f in Features)
        {
            switch (f.Status)
            {
                case FeatureStatuses.ToPlan:
                    KanbanFeaturesToPlan.Add(f);
                    break;
                case FeatureStatuses.InProgress:
                    KanbanFeaturesInProgress.Add(f);
                    break;
                case FeatureStatuses.Done:
                    KanbanFeaturesDone.Add(f);
                    break;
                case FeatureStatuses.Released:
                    KanbanFeaturesReleased.Add(f);
                    break;
                default:
                    KanbanFeaturesToPlan.Add(f);
                    break;
            }
        }
    }

    /// <summary>看板落列：仅改状态，成功返回 null；失败返回供 Banner 展示的文案。</summary>
    public async Task<string?> MoveFeatureToColumnAsync(string featureId, string targetStatus)
    {
        try
        {
            ErrorBanner = "";
            if (string.IsNullOrEmpty(SelectedProjectId))
            {
                return "请先选择项目。";
            }

            var existing = await featureRepository.GetByIdAsync(featureId).ConfigureAwait(true)
                ?? throw new InvalidOperationException("特性不存在。");
            if (existing.Status == targetStatus)
            {
                return null;
            }

            if (!FeatureStatusTransitions.TryValidate(existing.Status, targetStatus, out var ruleErr))
            {
                return ruleErr ?? "状态流转异常，变更被拒绝。";
            }

            var now = Now();
            await featureRepository.UpdateAsync(new Feature
            {
                Id = existing.Id,
                ProjectId = existing.ProjectId,
                Name = existing.Name,
                Description = existing.Description,
                Status = targetStatus,
                Priority = existing.Priority,
                AcceptanceCriteria = existing.AcceptanceCriteria,
                TechStack = existing.TechStack,
                Notes = existing.Notes,
                DueDate = existing.DueDate,
                AttachmentsPlaceholder = existing.AttachmentsPlaceholder,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = now,
                IsDeleted = existing.IsDeleted,
                RowVersion = existing.RowVersion,
            }).ConfigureAwait(true);

            await RefreshFeaturesOnlyAsync().ConfigureAwait(true);
            ReselectFeatureById(featureId);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    [RelayCommand]
    private void SelectFeature(FeatureRowViewModel? row) => SelectedFeature = row;

    [RelayCommand]
    private void ClearSelection() => SelectedFeature = null;

    [RelayCommand]
    private Task SetFilterAllAsync()
    {
        StatusFilter = null;
        return RefreshFeaturesOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterToPlanAsync()
    {
        StatusFilter = FeatureStatuses.ToPlan;
        return RefreshFeaturesOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterInProgressAsync()
    {
        StatusFilter = FeatureStatuses.InProgress;
        return RefreshFeaturesOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterDoneAsync()
    {
        StatusFilter = FeatureStatuses.Done;
        return RefreshFeaturesOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterReleasedAsync()
    {
        StatusFilter = FeatureStatuses.Released;
        return RefreshFeaturesOnlyAsync();
    }

    [RelayCommand]
    private Task SetSortUpdatedDescAsync()
    {
        SortField = FeatureSortField.UpdatedAt;
        SortDescending = true;
        return RefreshFeaturesOnlyAsync();
    }

    [RelayCommand]
    private Task SetSortNameAscAsync()
    {
        SortField = FeatureSortField.Name;
        SortDescending = false;
        return RefreshFeaturesOnlyAsync();
    }

    [RelayCommand]
    private Task SetSortCreatedDescAsync()
    {
        SortField = FeatureSortField.CreatedAt;
        SortDescending = true;
        return RefreshFeaturesOnlyAsync();
    }

    [RelayCommand]
    private void RequestNewFeatureUi()
    {
        if (string.IsNullOrEmpty(SelectedProjectId))
        {
            ErrorBanner = "请先选择所属项目。";
            return;
        }

        NewFeatureUiRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RequestEditFeatureUi()
    {
        if (SelectedFeature is null)
        {
            return;
        }

        EditFeatureUiRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ApplyDetailStatusAsync()
    {
        if (SelectedFeature is null || string.IsNullOrEmpty(SelectedProjectId))
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            var existing = await featureRepository.GetByIdAsync(SelectedFeature.Id).ConfigureAwait(true)
                ?? throw new InvalidOperationException("特性不存在。");
            if (existing.Status == DetailStatusDraft)
            {
                return;
            }

            var now = Now();
            await featureRepository.UpdateAsync(new Feature
            {
                Id = existing.Id,
                ProjectId = existing.ProjectId,
                Name = existing.Name,
                Description = existing.Description,
                Status = DetailStatusDraft,
                Priority = existing.Priority,
                AcceptanceCriteria = existing.AcceptanceCriteria,
                TechStack = existing.TechStack,
                Notes = existing.Notes,
                DueDate = existing.DueDate,
                AttachmentsPlaceholder = existing.AttachmentsPlaceholder,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = now,
                IsDeleted = existing.IsDeleted,
                RowVersion = existing.RowVersion,
            }).ConfigureAwait(true);

            await RefreshAsync().ConfigureAwait(true);
            ReselectFeatureById(existing.Id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedFeature is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            if (await featureDeletionGuard.HasBlockingTasksAsync(SelectedFeature.Id).ConfigureAwait(true))
            {
                ErrorBanner = "该特性有关联任务，无法删除。";
                return;
            }

            await featureRepository.SoftDeleteAsync(SelectedFeature.Id).ConfigureAwait(true);
            SelectedFeature = null;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    public async Task CreateFeatureAsync(string name, string description, int priority)
    {
        if (string.IsNullOrEmpty(SelectedProjectId))
        {
            throw new InvalidOperationException("未选择项目。");
        }

        var n = FeatureFieldValidator.ValidateName(name);
        var d = FeatureFieldValidator.ValidateDescription(description);
        var pri = FeaturePriorities.Normalize(priority);
        var now = Now();
        var f = new Feature
        {
            Id = Guid.NewGuid().ToString("D"),
            ProjectId = SelectedProjectId,
            Name = n,
            Description = d,
            Status = FeatureStatuses.ToPlan,
            Priority = pri,
            AcceptanceCriteria = string.Empty,
            TechStack = string.Empty,
            Notes = string.Empty,
            DueDate = null,
            AttachmentsPlaceholder = null,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            RowVersion = 1,
        };

        await featureRepository.InsertAsync(f).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        ReselectFeatureById(f.Id);
    }

    public async Task UpdateFeatureAsync(
        string id,
        string name,
        string description,
        int priority,
        string status,
        string acceptanceCriteria,
        string techStack,
        string notes,
        string? dueDate)
    {
        var existing = await featureRepository.GetByIdAsync(id).ConfigureAwait(true)
            ?? throw new InvalidOperationException("特性不存在。");
        var n = FeatureFieldValidator.ValidateName(name);
        var d = FeatureFieldValidator.ValidateDescription(description);
        var ac = FeatureFieldValidator.ValidateLongText(acceptanceCriteria, "验收标准");
        var ts = FeatureFieldValidator.ValidateLongText(techStack, "技术栈");
        var nt = FeatureFieldValidator.ValidateLongText(notes, "备注");
        var pri = FeaturePriorities.Normalize(priority);
        var now = Now();

        await featureRepository.UpdateAsync(new Feature
        {
            Id = existing.Id,
            ProjectId = existing.ProjectId,
            Name = n,
            Description = d,
            Status = status,
            Priority = pri,
            AcceptanceCriteria = ac,
            TechStack = ts,
            Notes = nt,
            DueDate = string.IsNullOrWhiteSpace(dueDate) ? null : dueDate.Trim(),
            AttachmentsPlaceholder = existing.AttachmentsPlaceholder,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = now,
            IsDeleted = existing.IsDeleted,
            RowVersion = existing.RowVersion,
        }).ConfigureAwait(true);

        await RefreshAsync().ConfigureAwait(true);
        ReselectFeatureById(id);
    }

    private void ReselectFeatureById(string id)
    {
        SelectedFeature = Features.FirstOrDefault(x => x.Id == id);
        if (SelectedFeature is not null)
        {
            RebuildAllowedStatuses(SelectedFeature.Status);
            DetailStatusDraft = SelectedFeature.Status;
        }
    }

    public async Task JumpToEntityFromSearchAsync(string featureId, string projectId)
    {
        SearchQuery = "";
        StatusFilter = null;
        ClearFeatureSearchHighlights();
        _suppressProjectRefreshForSearchJump = true;
        try
        {
            SelectedProjectId = projectId;
            await RefreshFeaturesOnlyAsync().ConfigureAwait(true);
        }
        finally
        {
            _suppressProjectRefreshForSearchJump = false;
        }

        var row = Features.FirstOrDefault(x => x.Id == featureId);
        if (row is null)
        {
            return;
        }

        row.IsSearchHighlight = true;
        SelectedFeature = row;
        ScheduleFeatureSearchHighlightClear();
    }

    private void ClearFeatureSearchHighlights()
    {
        foreach (var f in Features)
        {
            f.IsSearchHighlight = false;
        }

        foreach (var f in KanbanFeaturesToPlan)
        {
            f.IsSearchHighlight = false;
        }

        foreach (var f in KanbanFeaturesInProgress)
        {
            f.IsSearchHighlight = false;
        }

        foreach (var f in KanbanFeaturesDone)
        {
            f.IsSearchHighlight = false;
        }

        foreach (var f in KanbanFeaturesReleased)
        {
            f.IsSearchHighlight = false;
        }
    }

    private void ScheduleFeatureSearchHighlightClear()
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
        _searchHighlightClearTimer.Tick += OnFeatureSearchHighlightClearTick;
        _searchHighlightClearTimer.Start();
    }

    private void OnFeatureSearchHighlightClearTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnFeatureSearchHighlightClearTick;
        ClearFeatureSearchHighlights();
    }

    private static string Now() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string SortFieldToLabel(FeatureSortField f) => f switch
    {
        FeatureSortField.Name => "名称",
        FeatureSortField.CreatedAt => "创建时间",
        _ => "更新时间",
    };
}

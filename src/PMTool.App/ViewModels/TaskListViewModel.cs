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

public partial class TaskListViewModel(
    ITaskRepository taskRepository,
    IFeatureRepository featureRepository,
    IProjectRepository projectRepository) : ObservableObject, IOperationBarViewModel
{
    private bool _suppressTaskProjectCascadeForSearchJump;
    private DispatcherQueueTimer? _searchHighlightClearTimer;
    private DispatcherQueueTimer? _searchDebounceTimer;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _filterMenuItems;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _sortMenuItems;

    public string SearchPlaceholderText => "搜索任务名称或描述…";

    public bool IsOperationBarInteractive => SelectedFeatureId is { Length: > 0 };

    public string PrimaryActionLabel => "新建任务";

    public IRelayCommand? PrimaryActionCommand => RequestNewTaskUiCommand;

    public ReadOnlyObservableCollection<OperationBarMenuItem> FilterMenuItems =>
        _filterMenuItems ??= BuildFilterMenuItems();

    public ReadOnlyObservableCollection<OperationBarMenuItem> SortMenuItems =>
        _sortMenuItems ??= BuildSortMenuItems();

    public ObservableCollection<ProjectPickerItem> ProjectOptions { get; } = [];

    public ObservableCollection<FeaturePickerItem> FeatureOptions { get; } = [];

    public ObservableCollection<TaskRowViewModel> Tasks { get; } = [];

    public ObservableCollection<TaskRowViewModel> KanbanTasksNotStarted { get; } = [];

    public ObservableCollection<TaskRowViewModel> KanbanTasksInProgress { get; } = [];

    public ObservableCollection<TaskRowViewModel> KanbanTasksDone { get; } = [];

    public ObservableCollection<TaskRowViewModel> KanbanTasksCancelled { get; } = [];

    public ObservableCollection<string> DetailAllowedStatuses { get; } = [];

    [ObservableProperty]
    private string? _selectedProjectId;

    [ObservableProperty]
    private string? _selectedFeatureId;

    [ObservableProperty]
    private TaskRowViewModel? _selectedTask;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string? _statusFilter;

    [ObservableProperty]
    private TaskSortMode _sortMode = TaskSortMode.ManualOrder;

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    private string _errorBanner = "";

    [ObservableProperty]
    private bool _isKanbanView;

    [ObservableProperty]
    private string _detailStatusDraft = TaskStatuses.NotStarted;

    public string FilterButtonText => StatusFilter switch
    {
        null => "筛选：全部",
        TaskStatuses.NotStarted => "筛选：未开始",
        TaskStatuses.InProgress => "筛选：进行中",
        TaskStatuses.Done => "筛选：已完成",
        TaskStatuses.Cancelled => "筛选：已取消",
        _ => "筛选",
    };

    public string SortButtonText => SortMode switch
    {
        TaskSortMode.ManualOrder => "排序：手动顺序",
        TaskSortMode.Name => $"排序：名称 · {(SortDescending ? "降序" : "升序")}",
        _ => $"排序：更新时间 · {(SortDescending ? "降序" : "升序")}",
    };

    public Visibility ErrorBannerVisibility =>
        string.IsNullOrEmpty(ErrorBanner) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DetailPanelVisibility =>
        SelectedTask is null ? Visibility.Collapsed : Visibility.Visible;

    public bool ShowProjectsMissing => ProjectOptions.Count == 0;

    public bool ShowSelectProject => ProjectOptions.Count > 0 && string.IsNullOrEmpty(SelectedProjectId);

    public bool ShowSelectFeature =>
        !string.IsNullOrEmpty(SelectedProjectId) && string.IsNullOrEmpty(SelectedFeatureId);

    public bool ShowGlobalTaskEmpty =>
        !string.IsNullOrEmpty(SelectedFeatureId) && Tasks.Count == 0
        && string.IsNullOrWhiteSpace(SearchQuery) && StatusFilter is null;

    public bool ShowNoMatch =>
        !string.IsNullOrEmpty(SelectedFeatureId) && Tasks.Count == 0
        && (!string.IsNullOrWhiteSpace(SearchQuery) || StatusFilter is not null);

    public Visibility ListVisibility => Tasks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TableHeaderVisibility => IsKanbanView ? Visibility.Collapsed : Visibility.Visible;

    public Visibility TableMainVisibility =>
        !IsKanbanView && ListVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility KanbanMainVisibility =>
        IsKanbanView && ListVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyProjectsVisibility => ShowProjectsMissing ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectProjectVisibility => ShowSelectProject ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectFeatureVisibility => ShowSelectFeature ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GlobalEmptyVisibility => ShowGlobalTaskEmpty ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoMatchVisibility => ShowNoMatch ? Visibility.Visible : Visibility.Collapsed;

    public string DetailTitle => SelectedTask?.Name ?? string.Empty;

    public string DetailMeta =>
        SelectedTask is { } t
            ? $"{t.TaskType} · {t.Status} · {t.SeverityDisplay} · 估时{t.EstimatedHours:0.##}h · 更新 {t.UpdatedAt}"
            : string.Empty;

    public event EventHandler? NewTaskUiRequested;

    public event EventHandler? EditTaskUiRequested;

    public Task<PmTask?> GetTaskEntityAsync(string id) => taskRepository.GetByIdAsync(id);

    partial void OnSelectedProjectIdChanged(string? value)
    {
        OnPropertyChanged(nameof(IsOperationBarInteractive));
        OnPropertyChanged(nameof(ShowSelectProject));
        OnPropertyChanged(nameof(SelectProjectVisibility));
        OnPropertyChanged(nameof(ShowSelectFeature));
        OnPropertyChanged(nameof(SelectFeatureVisibility));
        if (_suppressTaskProjectCascadeForSearchJump)
        {
            return;
        }

        SelectedFeatureId = null;
        FeatureOptions.Clear();
        _ = LoadFeaturesForProjectAsync();
    }

    partial void OnSelectedFeatureIdChanged(string? value)
    {
        OnPropertyChanged(nameof(IsOperationBarInteractive));
        OnPropertyChanged(nameof(ShowSelectFeature));
        OnPropertyChanged(nameof(SelectFeatureVisibility));
        SelectedTask = null;
        _ = RefreshTasksOnlyAsync();
    }

    partial void OnSelectedTaskChanged(TaskRowViewModel? value)
    {
        if (value is null)
        {
            DetailAllowedStatuses.Clear();
            DetailStatusDraft = TaskStatuses.NotStarted;
        }
        else
        {
            RebuildAllowedStatuses(value.Status);
            DetailStatusDraft = value.Status;
        }

        OnPropertyChanged(nameof(DetailPanelVisibility));
        OnPropertyChanged(nameof(DetailTitle));
        OnPropertyChanged(nameof(DetailMeta));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
        OnPropertyChanged(nameof(CanReorder));
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
        OnPropertyChanged(nameof(CanReorder));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
        _ = RefreshTasksOnlyAsync();
    }

    partial void OnSortModeChanged(TaskSortMode value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        OnPropertyChanged(nameof(CanReorder));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
        _ = RefreshTasksOnlyAsync();
    }

    partial void OnSortDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = RefreshTasksOnlyAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(CanReorder));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
        ScheduleSearchRefresh();
    }

    public bool CanReorder =>
        SortMode == TaskSortMode.ManualOrder && StatusFilter is null && string.IsNullOrWhiteSpace(SearchQuery);

    public bool CanMoveUp =>
        CanReorder && SelectedTask is not null && Tasks.Count > 0 && Tasks[0].Id != SelectedTask.Id;

    public bool CanMoveDown =>
        CanReorder && SelectedTask is not null && Tasks.Count > 0 && Tasks[^1].Id != SelectedTask.Id;

    private void RebuildAllowedStatuses(string currentStatus)
    {
        DetailAllowedStatuses.Clear();
        foreach (var s in TaskStatusTransitions.GetAllowedTargets(currentStatus))
        {
            DetailAllowedStatuses.Add(s);
        }
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildFilterMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "全部", Command = SetFilterAllCommand },
            new() { Text = "未开始", Command = SetFilterNotStartedCommand },
            new() { Text = "进行中", Command = SetFilterInProgressCommand },
            new() { Text = "已完成", Command = SetFilterDoneCommand },
            new() { Text = "已取消", Command = SetFilterCancelledCommand },
        };
        return new ReadOnlyObservableCollection<OperationBarMenuItem>(items);
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildSortMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "手动顺序", Command = SetSortManualCommand },
            new() { Text = "更新时间 · 降序", Command = SetSortUpdatedDescCommand },
            new() { Text = "名称 · 升序", Command = SetSortNameAscCommand },
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
        await RefreshTasksOnlyAsync().ConfigureAwait(true);
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
                ProjectOptions.Add(new ProjectPickerItem { Id = item.Project.Id, Name = item.Project.Name });
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

            await LoadFeaturesForProjectAsync().ConfigureAwait(true);
            OnPropertyChanged(nameof(IsOperationBarInteractive));
            OnPropertyChanged(nameof(ShowProjectsMissing));
            OnPropertyChanged(nameof(ShowSelectProject));
            OnPropertyChanged(nameof(EmptyProjectsVisibility));
            OnPropertyChanged(nameof(SelectProjectVisibility));
            OnPropertyChanged(nameof(SelectFeatureVisibility));
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private async Task LoadFeaturesForProjectAsync()
    {
        if (string.IsNullOrEmpty(SelectedProjectId))
        {
            FeatureOptions.Clear();
            SelectedFeatureId = null;
            return;
        }

        var fq = new FeatureListQuery
        {
            ProjectId = SelectedProjectId!,
            SearchText = null,
            StatusFilter = null,
            SortField = FeatureSortField.UpdatedAt,
            SortDescending = true,
        };
        var flist = await featureRepository.ListAsync(fq).ConfigureAwait(true);
        FeatureOptions.Clear();
        foreach (var f in flist)
        {
            FeatureOptions.Add(new FeaturePickerItem { Id = f.Id, Name = f.Name });
        }

        if (FeatureOptions.Count > 0)
        {
            if (string.IsNullOrEmpty(SelectedFeatureId)
                || FeatureOptions.All(x => x.Id != SelectedFeatureId))
            {
                SelectedFeatureId = FeatureOptions[0].Id;
            }
            else
            {
                await RefreshTasksOnlyAsync().ConfigureAwait(true);
            }
        }
        else
        {
            SelectedFeatureId = null;
            Tasks.Clear();
            SelectedTask = null;
        }

        OnPropertyChanged(nameof(ShowSelectFeature));
        OnPropertyChanged(nameof(SelectFeatureVisibility));
        OnPropertyChanged(nameof(ShowGlobalTaskEmpty));
        OnPropertyChanged(nameof(ShowNoMatch));
        OnPropertyChanged(nameof(GlobalEmptyVisibility));
        OnPropertyChanged(nameof(NoMatchVisibility));
        OnPropertyChanged(nameof(ListVisibility));
        OnPropertyChanged(nameof(TableMainVisibility));
        OnPropertyChanged(nameof(KanbanMainVisibility));
        RebuildKanbanColumns();
    }

    private async Task RefreshTasksOnlyAsync()
    {
        ErrorBanner = "";
        try
        {
            if (string.IsNullOrEmpty(SelectedFeatureId))
            {
                Tasks.Clear();
                SelectedTask = null;
                RebuildKanbanColumns();
            }
            else
            {
                var q = new TaskListQuery
                {
                    FeatureId = SelectedFeatureId!,
                    SearchText = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim(),
                    StatusFilter = StatusFilter,
                    SortMode = SortMode,
                    SortDescending = SortDescending,
                };
                var list = await taskRepository.ListAsync(q).ConfigureAwait(true);
                Tasks.Clear();
                foreach (var t in list)
                {
                    Tasks.Add(TaskRowViewModel.FromTask(t));
                }
            }

            OnPropertyChanged(nameof(ShowGlobalTaskEmpty));
            OnPropertyChanged(nameof(ShowNoMatch));
            OnPropertyChanged(nameof(GlobalEmptyVisibility));
            OnPropertyChanged(nameof(NoMatchVisibility));
            OnPropertyChanged(nameof(ListVisibility));
            OnPropertyChanged(nameof(TableMainVisibility));
            OnPropertyChanged(nameof(KanbanMainVisibility));
            OnPropertyChanged(nameof(CanMoveUp));
            OnPropertyChanged(nameof(CanMoveDown));
            OnPropertyChanged(nameof(CanReorder));
            RebuildKanbanColumns();
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private void RebuildKanbanColumns()
    {
        KanbanTasksNotStarted.Clear();
        KanbanTasksInProgress.Clear();
        KanbanTasksDone.Clear();
        KanbanTasksCancelled.Clear();
        foreach (var t in Tasks)
        {
            switch (t.Status)
            {
                case TaskStatuses.NotStarted:
                    KanbanTasksNotStarted.Add(t);
                    break;
                case TaskStatuses.InProgress:
                    KanbanTasksInProgress.Add(t);
                    break;
                case TaskStatuses.Done:
                    KanbanTasksDone.Add(t);
                    break;
                case TaskStatuses.Cancelled:
                    KanbanTasksCancelled.Add(t);
                    break;
                default:
                    KanbanTasksNotStarted.Add(t);
                    break;
            }
        }
    }

    /// <summary>看板落列：仅改状态，不改 sort_value；成功返回 null。</summary>
    public async Task<string?> MoveTaskToColumnAsync(string taskId, string targetStatus)
    {
        try
        {
            ErrorBanner = "";
            if (string.IsNullOrEmpty(SelectedFeatureId))
            {
                return "请先选择特性。";
            }

            var existing = await taskRepository.GetByIdAsync(taskId).ConfigureAwait(true)
                ?? throw new InvalidOperationException("任务不存在。");
            if (existing.Status == targetStatus)
            {
                return null;
            }

            if (!TaskStatusTransitions.TryValidate(existing.Status, targetStatus, out var ruleErr))
            {
                return ruleErr ?? "状态流转异常，变更被拒绝。";
            }

            var now = Now();
            await taskRepository.UpdateAsync(new PmTask
            {
                Id = existing.Id,
                ProjectId = existing.ProjectId,
                FeatureId = existing.FeatureId,
                Name = existing.Name,
                Description = existing.Description,
                TaskType = existing.TaskType,
                Status = targetStatus,
                Severity = existing.Severity,
                EstimatedHours = existing.EstimatedHours,
                ActualHours = existing.ActualHours,
                CompletedAt = existing.CompletedAt,
                SortValue = existing.SortValue,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = now,
                IsDeleted = existing.IsDeleted,
                RowVersion = existing.RowVersion,
            }).ConfigureAwait(true);

            await RefreshTasksOnlyAsync().ConfigureAwait(true);
            ReselectTaskById(taskId);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    [RelayCommand]
    private void SelectTaskRow(TaskRowViewModel? row)
    {
        SelectedTask = row;
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
    }

    [RelayCommand]
    private void ClearTaskSelection() => SelectedTask = null;

    [RelayCommand]
    private Task SetFilterAllAsync()
    {
        StatusFilter = null;
        return RefreshTasksOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterNotStartedAsync()
    {
        StatusFilter = TaskStatuses.NotStarted;
        return RefreshTasksOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterInProgressAsync()
    {
        StatusFilter = TaskStatuses.InProgress;
        return RefreshTasksOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterDoneAsync()
    {
        StatusFilter = TaskStatuses.Done;
        return RefreshTasksOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterCancelledAsync()
    {
        StatusFilter = TaskStatuses.Cancelled;
        return RefreshTasksOnlyAsync();
    }

    [RelayCommand]
    private Task SetSortManualAsync()
    {
        SortMode = TaskSortMode.ManualOrder;
        return RefreshTasksOnlyAsync();
    }

    [RelayCommand]
    private Task SetSortUpdatedDescAsync()
    {
        SortMode = TaskSortMode.UpdatedAt;
        SortDescending = true;
        return RefreshTasksOnlyAsync();
    }

    [RelayCommand]
    private Task SetSortNameAscAsync()
    {
        SortMode = TaskSortMode.Name;
        SortDescending = false;
        return RefreshTasksOnlyAsync();
    }

    [RelayCommand]
    private void RequestNewTaskUi()
    {
        if (string.IsNullOrEmpty(SelectedFeatureId))
        {
            ErrorBanner = "请先选择所属特性。";
            return;
        }

        NewTaskUiRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RequestEditTaskUi()
    {
        if (SelectedTask is null)
        {
            return;
        }

        EditTaskUiRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ApplyDetailStatusAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            var existing = await taskRepository.GetByIdAsync(SelectedTask.Id).ConfigureAwait(true)
                ?? throw new InvalidOperationException("任务不存在。");
            if (existing.Status == DetailStatusDraft)
            {
                return;
            }

            var now = Now();
            await taskRepository.UpdateAsync(new PmTask
            {
                Id = existing.Id,
                ProjectId = existing.ProjectId,
                FeatureId = existing.FeatureId,
                Name = existing.Name,
                Description = existing.Description,
                TaskType = existing.TaskType,
                Status = DetailStatusDraft,
                Severity = existing.Severity,
                EstimatedHours = existing.EstimatedHours,
                ActualHours = existing.ActualHours,
                CompletedAt = existing.CompletedAt,
                SortValue = existing.SortValue,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = now,
                IsDeleted = existing.IsDeleted,
                RowVersion = existing.RowVersion,
            }).ConfigureAwait(true);

            await RefreshAsync().ConfigureAwait(true);
            ReselectTaskById(existing.Id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private async Task MoveUpAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            await taskRepository.MoveWithinFeatureAsync(SelectedTask.Id, -1).ConfigureAwait(true);
            var id = SelectedTask.Id;
            SortMode = TaskSortMode.ManualOrder;
            await RefreshTasksOnlyAsync().ConfigureAwait(true);
            ReselectTaskById(id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private async Task MoveDownAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            await taskRepository.MoveWithinFeatureAsync(SelectedTask.Id, 1).ConfigureAwait(true);
            var id = SelectedTask.Id;
            SortMode = TaskSortMode.ManualOrder;
            await RefreshTasksOnlyAsync().ConfigureAwait(true);
            ReselectTaskById(id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            await taskRepository.SoftDeleteAsync(SelectedTask.Id).ConfigureAwait(true);
            SelectedTask = null;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    public async Task CreateTaskAsync(
        string name,
        string description,
        string taskType,
        string? severity,
        double estimatedHours)
    {
        if (string.IsNullOrEmpty(SelectedFeatureId))
        {
            throw new InvalidOperationException("未选择特性。");
        }

        var n = TaskFieldValidator.ValidateName(name);
        var d = TaskFieldValidator.ValidateDescription(description);
        var eh = TaskFieldValidator.ValidateEstimatedHours(estimatedHours);
        var sev = TaskSeverityRules.NormalizeForPersistence(taskType, severity);
        var now = Now();
        var t = new PmTask
        {
            Id = Guid.NewGuid().ToString("D"),
            ProjectId = string.Empty,
            FeatureId = SelectedFeatureId,
            Name = n,
            Description = d,
            TaskType = taskType,
            Status = TaskStatuses.NotStarted,
            Severity = sev,
            EstimatedHours = eh,
            ActualHours = 0,
            CompletedAt = null,
            SortValue = 0,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            RowVersion = 1,
        };

        await taskRepository.InsertAsync(t).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        ReselectTaskById(t.Id);
    }

    public async Task UpdateTaskAsync(
        string id,
        string name,
        string description,
        string taskType,
        string? severity,
        double estimatedHours,
        double actualHours,
        string status)
    {
        var existing = await taskRepository.GetByIdAsync(id).ConfigureAwait(true)
            ?? throw new InvalidOperationException("任务不存在。");
        var n = TaskFieldValidator.ValidateName(name);
        var d = TaskFieldValidator.ValidateDescription(description);
        var eh = TaskFieldValidator.ValidateEstimatedHours(estimatedHours);
        var ah = TaskFieldValidator.ValidateActualHours(actualHours);
        if (!TaskSeverityRules.TryValidate(taskType, severity, out var sevErr))
        {
            throw new InvalidOperationException(sevErr);
        }

        var sev = TaskSeverityRules.NormalizeForPersistence(taskType, severity);
        var now = Now();
        await taskRepository.UpdateAsync(new PmTask
        {
            Id = existing.Id,
            ProjectId = existing.ProjectId,
            FeatureId = existing.FeatureId,
            Name = n,
            Description = d,
            TaskType = taskType,
            Status = status,
            Severity = sev,
            EstimatedHours = eh,
            ActualHours = ah,
            CompletedAt = existing.CompletedAt,
            SortValue = existing.SortValue,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = now,
            IsDeleted = existing.IsDeleted,
            RowVersion = existing.RowVersion,
        }).ConfigureAwait(true);

        await RefreshAsync().ConfigureAwait(true);
        ReselectTaskById(id);
    }

    private void ReselectTaskById(string id)
    {
        SelectedTask = Tasks.FirstOrDefault(x => x.Id == id);
        if (SelectedTask is not null)
        {
            RebuildAllowedStatuses(SelectedTask.Status);
            DetailStatusDraft = SelectedTask.Status;
        }
    }

    public async Task JumpToEntityFromSearchAsync(string taskId, string projectId, string? featureId)
    {
        SearchQuery = "";
        StatusFilter = null;
        ClearTaskSearchHighlights();
        _suppressTaskProjectCascadeForSearchJump = true;
        try
        {
            SelectedProjectId = projectId;
            SelectedFeatureId = null;
            await LoadFeaturesForProjectAsync().ConfigureAwait(true);
            var fid = featureId;
            if (string.IsNullOrEmpty(fid))
            {
                var t = await taskRepository.GetByIdAsync(taskId).ConfigureAwait(true);
                fid = t?.FeatureId;
            }

            if (!string.IsNullOrEmpty(fid) && FeatureOptions.Any(o => o.Id == fid))
            {
                SelectedFeatureId = fid;
            }

            await RefreshTasksOnlyAsync().ConfigureAwait(true);
        }
        finally
        {
            _suppressTaskProjectCascadeForSearchJump = false;
        }

        var row = Tasks.FirstOrDefault(x => x.Id == taskId);
        if (row is null)
        {
            return;
        }

        row.IsSearchHighlight = true;
        SelectedTask = row;
        ScheduleTaskSearchHighlightClear();
    }

    private void ClearTaskSearchHighlights()
    {
        foreach (var t in Tasks)
        {
            t.IsSearchHighlight = false;
        }

        foreach (var c in KanbanTasksNotStarted)
        {
            c.IsSearchHighlight = false;
        }

        foreach (var c in KanbanTasksInProgress)
        {
            c.IsSearchHighlight = false;
        }

        foreach (var c in KanbanTasksDone)
        {
            c.IsSearchHighlight = false;
        }

        foreach (var c in KanbanTasksCancelled)
        {
            c.IsSearchHighlight = false;
        }
    }

    private void ScheduleTaskSearchHighlightClear()
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
        _searchHighlightClearTimer.Tick += OnTaskSearchHighlightClearTick;
        _searchHighlightClearTimer.Start();
    }

    private void OnTaskSearchHighlightClearTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnTaskSearchHighlightClearTick;
        ClearTaskSearchHighlights();
    }

    private static string Now() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}

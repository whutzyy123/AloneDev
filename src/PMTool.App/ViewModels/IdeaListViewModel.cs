using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PMTool.App.Models;
using PMTool.App.Services;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;
using PMTool.Core.Validation;

namespace PMTool.App.ViewModels;

public partial class IdeaListViewModel(
    IIdeaRepository ideaRepository,
    IProjectRepository projectRepository,
    ITaskRepository taskRepository,
    IFeatureRepository featureRepository,
    Func<IShellNavCoordinator> getShellNavCoordinator,
    TaskListViewModel taskListViewModel) : ObservableObject, IOperationBarViewModel
{
    private bool _detailLoadSilence;
    private DispatcherQueueTimer? _searchHighlightClearTimer;
    private DispatcherQueueTimer? _searchDebounceTimer;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _filterMenuItems;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _priorityFilterMenuItems;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _sortMenuItems;

    public ObservableCollection<IdeaRowViewModel> Ideas { get; } = [];

    public ObservableCollection<IdeaDocLinkRowViewModel> DetailDocLinks { get; } = [];

    public ObservableCollection<ProjectPickerItem> ProjectOptions { get; } = [];

    [ObservableProperty]
    private IdeaRowViewModel? _selectedIdeaRow;

    [ObservableProperty]
    private Idea? _detailIdea;

    [ObservableProperty]
    private string _errorBanner = "";

    [ObservableProperty]
    private string? _statusFilter;

    [ObservableProperty]
    private string? _priorityFilter;

    [ObservableProperty]
    private IdeaSortField _sortField = IdeaSortField.UpdatedAt;

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string? _detailLinkedProjectId;

    [ObservableProperty]
    private string _detailStatusSelection = IdeaStatuses.Pending;

    /// <summary>空字符串表示无优先级。</summary>
    [ObservableProperty]
    private string _detailPrioritySelection = "";

    public IReadOnlyList<string> IdeaStatusOptions => IdeaStatuses.All;

    public IReadOnlyList<string> IdeaPriorityPickerValues { get; } =
    [
        "",
        IdeaPriorities.P0,
        IdeaPriorities.P1,
        IdeaPriorities.P2,
        IdeaPriorities.P3,
    ];

    public string SearchPlaceholderText => "搜索灵感标题、描述、技术栈…";

    public bool IsOperationBarInteractive => true;

    public string PrimaryActionLabel => "新建灵感";

    public IRelayCommand? PrimaryActionCommand => RequestNewIdeaUiCommand;

    public ReadOnlyObservableCollection<OperationBarMenuItem> FilterMenuItems =>
        _filterMenuItems ??= BuildStatusFilterMenuItems();

    public ReadOnlyObservableCollection<OperationBarMenuItem> PriorityFilterMenuItems =>
        _priorityFilterMenuItems ??= BuildPriorityFilterMenuItems();

    public ReadOnlyObservableCollection<OperationBarMenuItem> SortMenuItems =>
        _sortMenuItems ??= BuildSortMenuItems();

    public Visibility ErrorBannerVisibility =>
        string.IsNullOrEmpty(ErrorBanner) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DetailPanelVisibility =>
        SelectedIdeaRow is not null ? Visibility.Visible : Visibility.Collapsed;

    public bool CanConvertToTask => DetailIdea is not null;

    public Visibility ListVisibility => Ideas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GlobalEmptyVisibility =>
        Ideas.Count == 0 && string.IsNullOrWhiteSpace(SearchQuery) && StatusFilter is null && PriorityFilter is null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility NoMatchVisibility =>
        Ideas.Count == 0 && (!string.IsNullOrWhiteSpace(SearchQuery) || StatusFilter is not null || PriorityFilter is not null)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string FilterButtonText => StatusFilter switch
    {
        null => "状态：全部",
        IdeaStatuses.Pending => "状态：待评估",
        IdeaStatuses.Approved => "状态：已立项",
        IdeaStatuses.Shelved => "状态：已搁置",
        _ => "状态",
    };

    public string PriorityFilterButtonText => PriorityFilter switch
    {
        null => "优先级：全部",
        IdeaPriorities.P0 => "优先级：P0",
        IdeaPriorities.P1 => "优先级：P1",
        IdeaPriorities.P2 => "优先级：P2",
        IdeaPriorities.P3 => "优先级：P3",
        _ => "优先级",
    };

    public string SortButtonText => SortField switch
    {
        IdeaSortField.Title => SortDescending ? "排序：标题 · 降序" : "排序：标题 · 升序",
        IdeaSortField.Status => SortDescending ? "排序：状态 · 降序" : "排序：状态 · 升序",
        _ => SortDescending ? "排序：更新时间 · 降序" : "排序：更新时间 · 升序",
    };

    [RelayCommand]
    private void ClearIdeaSelection() => SelectedIdeaRow = null;

    public bool CanEditLinkedProject => DetailIdea?.Status == IdeaStatuses.Approved;

    public event EventHandler? NewIdeaUiRequested;

    public event EventHandler? EditIdeaUiRequested;

    public event EventHandler? AddDocumentsUiRequested;

    public event EventHandler? ConvertToTaskUiRequested;

    partial void OnErrorBannerChanged(string value) => OnPropertyChanged(nameof(ErrorBannerVisibility));

    partial void OnSelectedIdeaRowChanged(IdeaRowViewModel? value)
    {
        OnPropertyChanged(nameof(DetailPanelVisibility));
        OnPropertyChanged(nameof(CanConvertToTask));
        _ = LoadDetailAsync();
    }

    partial void OnDetailIdeaChanged(Idea? value)
    {
        OnPropertyChanged(nameof(CanEditLinkedProject));
        OnPropertyChanged(nameof(CanConvertToTask));
        DetailLinkedProjectId = value?.LinkedProjectId;
    }

    partial void OnDetailStatusSelectionChanged(string value)
    {
        if (_detailLoadSilence || DetailIdea is null || value == DetailIdea.Status)
        {
            return;
        }

        _ = CommitDetailStatusAsync(value);
    }

    partial void OnDetailPrioritySelectionChanged(string value)
    {
        if (_detailLoadSilence || DetailIdea is null)
        {
            return;
        }

        var pr = IdeaFieldValidator.ValidatePriority(string.IsNullOrWhiteSpace(value) ? null : value);
        var cur = DetailIdea.Priority;
        if (pr == cur || (pr is null && cur is null))
        {
            return;
        }

        _ = CommitDetailPriorityAsync(pr);
    }

    partial void OnStatusFilterChanged(string? value)
    {
        OnPropertyChanged(nameof(FilterButtonText));
        _ = RefreshIdeasOnlyAsync();
    }

    partial void OnPriorityFilterChanged(string? value)
    {
        OnPropertyChanged(nameof(PriorityFilterButtonText));
        _ = RefreshIdeasOnlyAsync();
    }

    partial void OnSortFieldChanged(IdeaSortField value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = RefreshIdeasOnlyAsync();
    }

    partial void OnSortDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = RefreshIdeasOnlyAsync();
    }

    partial void OnSearchQueryChanged(string value) => ScheduleSearchRefresh();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        ErrorBanner = "";
        try
        {
            var pq = new ProjectListQuery(null, null, ProjectSortField.Name, false);
            var plist = await projectRepository.ListAsync(pq).ConfigureAwait(true);
            ProjectOptions.Clear();
            foreach (var item in plist)
            {
                ProjectOptions.Add(new ProjectPickerItem { Id = item.Project.Id, Name = item.Project.Name });
            }

            await RefreshIdeasOnlyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private async Task RefreshIdeasOnlyAsync()
    {
        ErrorBanner = "";
        try
        {
            var prevId = SelectedIdeaRow?.Id;
            var q = new IdeaListQuery(
                string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim(),
                StatusFilter,
                PriorityFilter,
                SortField,
                SortDescending);
            var list = await ideaRepository.ListAsync(q).ConfigureAwait(true);
            Ideas.Clear();
            foreach (var idea in list)
            {
                Ideas.Add(IdeaRowViewModel.FromIdea(idea));
            }

            if (prevId is { Length: > 0 } pid)
            {
                SelectedIdeaRow = Ideas.FirstOrDefault(i => i.Id == pid);
            }
            else
            {
                SelectedIdeaRow = null;
                DetailIdeaWrapper(null);
            }

            OnPropertyChanged(nameof(ListVisibility));
            OnPropertyChanged(nameof(GlobalEmptyVisibility));
            OnPropertyChanged(nameof(NoMatchVisibility));
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private async Task LoadDetailAsync()
    {
        DetailDocLinks.Clear();
        if (SelectedIdeaRow is null)
        {
            _detailLoadSilence = true;
            try
            {
                DetailIdeaWrapper(null);
                DetailStatusSelection = IdeaStatuses.Pending;
                DetailPrioritySelection = "";
            }
            finally
            {
                _detailLoadSilence = false;
            }

            return;
        }

        try
        {
            var idea = await ideaRepository.GetByIdAsync(SelectedIdeaRow.Id).ConfigureAwait(true);
            if (idea is null)
            {
                _detailLoadSilence = true;
                try
                {
                    DetailIdeaWrapper(null);
                    DetailStatusSelection = IdeaStatuses.Pending;
                    DetailPrioritySelection = "";
                }
                finally
                {
                    _detailLoadSilence = false;
                }

                return;
            }

            _detailLoadSilence = true;
            try
            {
                DetailIdeaWrapper(idea);
                DetailStatusSelection = idea.Status;
                DetailPrioritySelection = idea.Priority ?? "";
            }
            finally
            {
                _detailLoadSilence = false;
            }

            var links = await ideaRepository.ListDocumentLinksAsync(idea.Id).ConfigureAwait(true);
            foreach (var l in links)
            {
                DetailDocLinks.Add(new IdeaDocLinkRowViewModel
                {
                    LinkId = l.Id,
                    DocumentId = l.DocumentId,
                    DocumentName = string.IsNullOrEmpty(l.DocumentName) ? l.DocumentId : l.DocumentName,
                    RowVersion = l.RowVersion,
                });
            }
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private void DetailIdeaWrapper(Idea? idea)
    {
        DetailIdea = idea;
        DetailLinkedProjectId = idea?.LinkedProjectId;
    }

    [RelayCommand]
    private void SelectIdeaRow(IdeaRowViewModel? row) => SelectedIdeaRow = row;

    [RelayCommand]
    private void RequestNewIdeaUi() => NewIdeaUiRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RequestEditIdeaUi()
    {
        if (SelectedIdeaRow is null)
        {
            return;
        }

        EditIdeaUiRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RequestAddDocumentsUi()
    {
        if (SelectedIdeaRow is null)
        {
            return;
        }

        AddDocumentsUiRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RequestConvertToTaskUi()
    {
        if (DetailIdea is null)
        {
            return;
        }

        ConvertToTaskUiRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>供「转化为任务」对话框填充模块下拉框。</summary>
    public async Task LoadFeatureOptionsForProjectAsync(string projectId, ObservableCollection<FeaturePickerItem> target)
    {
        target.Clear();
        target.Add(new FeaturePickerItem { Id = string.Empty, Name = "无模块（仅项目）" });
        if (string.IsNullOrEmpty(projectId))
        {
            return;
        }

        var fq = new FeatureListQuery
        {
            ProjectId = projectId,
            SearchText = null,
            StatusFilter = null,
            SortField = FeatureSortField.UpdatedAt,
            SortDescending = true,
        };
        var flist = await featureRepository.ListAsync(fq).ConfigureAwait(true);
        foreach (var f in flist)
        {
            target.Add(new FeaturePickerItem { Id = f.Id, Name = f.Name });
        }
    }

    public async Task CreateTaskFromSelectedIdeaAsync(
        string projectId,
        string? featureIdOrEmpty,
        string taskType,
        string? severity,
        double estimatedHours)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            throw new InvalidOperationException("请选择项目。");
        }

        if (DetailIdea is null || SelectedIdeaRow is null)
        {
            throw new InvalidOperationException("未选择灵感。");
        }

        var idea = await ideaRepository.GetByIdAsync(SelectedIdeaRow.Id).ConfigureAwait(true)
            ?? throw new InvalidOperationException("灵感不存在或已删除。");

        var titleTrim = IdeaFieldValidator.ValidateTitle(idea.Title);
        if (titleTrim.Length > 100)
        {
            titleTrim = titleTrim[..100];
        }

        var name = TaskFieldValidator.ValidateName(titleTrim);
        var descBase = IdeaFieldValidator.ValidateDescription(idea.Description);
        var tech = IdeaFieldValidator.ValidateTechStack(idea.TechStack);
        var desc = string.IsNullOrWhiteSpace(tech)
            ? descBase
            : (string.IsNullOrWhiteSpace(descBase) ? $"技术栈：{tech}" : $"{descBase}\n\n技术栈：{tech}");
        if (desc.Length > 500)
        {
            desc = desc[..499] + "…";
        }

        desc = TaskFieldValidator.ValidateDescription(desc);

        var eh = TaskFieldValidator.ValidateEstimatedHours(estimatedHours);
        var sev = TaskSeverityRules.NormalizeForPersistence(taskType, severity);

        var featureId = string.IsNullOrEmpty(featureIdOrEmpty) ? null : featureIdOrEmpty;
        var now = Now();
        var taskId = Guid.NewGuid().ToString("D");
        var task = new PmTask
        {
            Id = taskId,
            ProjectId = projectId,
            FeatureId = featureId,
            Name = name,
            Description = desc,
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

        await taskRepository.InsertAsync(task).ConfigureAwait(true);

        IdeaFieldValidator.ValidateLinkedProject(IdeaStatuses.Approved, projectId);
        var linked = IdeaFieldValidator.NormalizeLinkedProjectId(IdeaStatuses.Approved, projectId);
        var nextIdea = new Idea
        {
            Id = idea.Id,
            Title = idea.Title,
            Description = idea.Description,
            TechStack = idea.TechStack,
            Status = IdeaStatuses.Approved,
            Priority = idea.Priority,
            LinkedProjectId = linked,
            CreatedAt = idea.CreatedAt,
            UpdatedAt = Now(),
            IsDeleted = idea.IsDeleted,
            RowVersion = idea.RowVersion,
        };
        await ideaRepository.UpdateAsync(nextIdea).ConfigureAwait(true);

        taskListViewModel.QueueFocusNewTaskAfterRefresh(taskId, projectId, featureId);
        getShellNavCoordinator().ActivatePrimaryNav("tasks");
    }

    [RelayCommand]
    private async Task RemoveDocLinkAsync(IdeaDocLinkRowViewModel? row)
    {
        if (row is null || SelectedIdeaRow is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            await ideaRepository.RemoveDocumentLinkAsync(row.LinkId, row.RowVersion).ConfigureAwait(true);
            await LoadDetailAsync().ConfigureAwait(true);
            await RefreshIdeasOnlyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveLinkedProjectAsync()
    {
        if (DetailIdea is null)
        {
            return;
        }

        if (DetailIdea.Status != IdeaStatuses.Approved)
        {
            ErrorBanner = "仅「已立项」时可关联项目。";
            return;
        }

        try
        {
            ErrorBanner = "";
            var linkedRaw = string.IsNullOrWhiteSpace(DetailLinkedProjectId) ? null : DetailLinkedProjectId.Trim();
            IdeaFieldValidator.ValidateLinkedProject(DetailIdea.Status, linkedRaw);
            var linked = IdeaFieldValidator.NormalizeLinkedProjectId(DetailIdea.Status, linkedRaw);
            var next = new Idea
            {
                Id = DetailIdea.Id,
                Title = DetailIdea.Title,
                Description = DetailIdea.Description,
                TechStack = DetailIdea.TechStack,
                Status = DetailIdea.Status,
                Priority = DetailIdea.Priority,
                LinkedProjectId = linked,
                CreatedAt = DetailIdea.CreatedAt,
                UpdatedAt = Now(),
                IsDeleted = DetailIdea.IsDeleted,
                RowVersion = DetailIdea.RowVersion,
            };
            await ideaRepository.UpdateAsync(next).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SelectedIdeaRow = Ideas.FirstOrDefault(i => i.Id == next.Id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private async Task CommitDetailStatusAsync(string newStatus)
    {
        if (DetailIdea is null)
        {
            return;
        }

        var st = IdeaFieldValidator.ValidateStatus(newStatus);
        try
        {
            ErrorBanner = "";
            var linked = IdeaFieldValidator.NormalizeLinkedProjectId(st, DetailIdea.LinkedProjectId);
            var next = new Idea
            {
                Id = DetailIdea.Id,
                Title = DetailIdea.Title,
                Description = DetailIdea.Description,
                TechStack = DetailIdea.TechStack,
                Status = st,
                Priority = DetailIdea.Priority,
                LinkedProjectId = linked,
                CreatedAt = DetailIdea.CreatedAt,
                UpdatedAt = Now(),
                IsDeleted = DetailIdea.IsDeleted,
                RowVersion = DetailIdea.RowVersion,
            };
            await ideaRepository.UpdateAsync(next).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SelectedIdeaRow = Ideas.FirstOrDefault(i => i.Id == next.Id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
            _detailLoadSilence = true;
            try
            {
                DetailStatusSelection = DetailIdea.Status;
            }
            finally
            {
                _detailLoadSilence = false;
            }
        }
    }

    private async Task CommitDetailPriorityAsync(string? newPriority)
    {
        if (DetailIdea is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            var linked = IdeaFieldValidator.NormalizeLinkedProjectId(DetailIdea.Status, DetailIdea.LinkedProjectId);
            var next = new Idea
            {
                Id = DetailIdea.Id,
                Title = DetailIdea.Title,
                Description = DetailIdea.Description,
                TechStack = DetailIdea.TechStack,
                Status = DetailIdea.Status,
                Priority = newPriority,
                LinkedProjectId = linked,
                CreatedAt = DetailIdea.CreatedAt,
                UpdatedAt = Now(),
                IsDeleted = DetailIdea.IsDeleted,
                RowVersion = DetailIdea.RowVersion,
            };
            await ideaRepository.UpdateAsync(next).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SelectedIdeaRow = Ideas.FirstOrDefault(i => i.Id == next.Id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
            _detailLoadSilence = true;
            try
            {
                DetailPrioritySelection = DetailIdea.Priority ?? "";
            }
            finally
            {
                _detailLoadSilence = false;
            }
        }
    }

    public async Task CreateIdeaAsync(string title, string? description, string? techStack, string? priority)
    {
        var t = IdeaFieldValidator.ValidateTitle(title);
        var d = IdeaFieldValidator.ValidateDescription(description);
        var ts = IdeaFieldValidator.ValidateTechStack(techStack);
        var pr = IdeaFieldValidator.ValidatePriority(priority);
        var now = Now();
        var idea = new Idea
        {
            Id = Guid.NewGuid().ToString("D"),
            Title = t,
            Description = d,
            TechStack = ts,
            Status = IdeaStatuses.Pending,
            Priority = pr,
            LinkedProjectId = null,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            RowVersion = 1,
        };
        IdeaFieldValidator.ValidateForInsert(idea);
        await ideaRepository.InsertAsync(idea).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        SelectedIdeaRow = Ideas.FirstOrDefault(i => i.Id == idea.Id);
    }

    public async Task UpdateIdeaFromDialogAsync(
        string id,
        string title,
        string? description,
        string? techStack,
        string status,
        string? priority,
        string? linkedProjectId)
    {
        var existing = await ideaRepository.GetByIdAsync(id).ConfigureAwait(true)
            ?? throw new InvalidOperationException("灵感不存在或已删除。");
        var t = IdeaFieldValidator.ValidateTitle(title);
        var d = IdeaFieldValidator.ValidateDescription(description);
        var ts = IdeaFieldValidator.ValidateTechStack(techStack);
        var st = IdeaFieldValidator.ValidateStatus(status);
        var pr = IdeaFieldValidator.ValidatePriority(priority);
        var linkedRaw = string.IsNullOrWhiteSpace(linkedProjectId) ? null : linkedProjectId.Trim();
        IdeaFieldValidator.ValidateLinkedProject(st, linkedRaw);
        var linked = IdeaFieldValidator.NormalizeLinkedProjectId(st, linkedRaw);
        var next = new Idea
        {
            Id = existing.Id,
            Title = t,
            Description = d,
            TechStack = ts,
            Status = st,
            Priority = pr,
            LinkedProjectId = linked,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = Now(),
            IsDeleted = existing.IsDeleted,
            RowVersion = existing.RowVersion,
        };
        await ideaRepository.UpdateAsync(next).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        SelectedIdeaRow = Ideas.FirstOrDefault(i => i.Id == id);
    }

    public async Task DeleteSelectedAsync()
    {
        if (DetailIdea is null)
        {
            return;
        }

        await ideaRepository.SoftDeleteAsync(DetailIdea.Id, DetailIdea.RowVersion).ConfigureAwait(true);
        SelectedIdeaRow = null;
        DetailIdeaWrapper(null);
        await RefreshAsync().ConfigureAwait(true);
    }

    public async Task AddDocumentLinksAsync(IEnumerable<string> documentIds)
    {
        if (SelectedIdeaRow is null)
        {
            return;
        }

        foreach (var did in documentIds)
        {
            if (string.IsNullOrWhiteSpace(did))
            {
                continue;
            }

            try
            {
                await ideaRepository.AddDocumentLinkAsync(SelectedIdeaRow.Id, did.Trim()).ConfigureAwait(true);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("已与当前灵感关联"))
            {
                // 跳过重复
            }
        }

        await LoadDetailAsync().ConfigureAwait(true);
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
        await RefreshIdeasOnlyAsync().ConfigureAwait(true);
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildStatusFilterMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "全部", Command = SetStatusFilterAllCommand },
            new() { Text = IdeaStatuses.Pending, Command = SetStatusFilterPendingCommand },
            new() { Text = IdeaStatuses.Approved, Command = SetStatusFilterApprovedCommand },
            new() { Text = IdeaStatuses.Shelved, Command = SetStatusFilterShelvedCommand },
        };
        return new ReadOnlyObservableCollection<OperationBarMenuItem>(items);
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildPriorityFilterMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "全部", Command = SetPriorityFilterAllCommand },
            new() { Text = IdeaPriorities.P0, Command = SetPriorityFilterP0Command },
            new() { Text = IdeaPriorities.P1, Command = SetPriorityFilterP1Command },
            new() { Text = IdeaPriorities.P2, Command = SetPriorityFilterP2Command },
            new() { Text = IdeaPriorities.P3, Command = SetPriorityFilterP3Command },
        };
        return new ReadOnlyObservableCollection<OperationBarMenuItem>(items);
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildSortMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "更新时间 · 降序", Command = SetSortUpdatedDescCommand },
            new() { Text = "更新时间 · 升序", Command = SetSortUpdatedAscCommand },
            new() { Text = "标题 · 升序", Command = SetSortTitleAscCommand },
            new() { Text = "标题 · 降序", Command = SetSortTitleDescCommand },
            new() { Text = "状态 · 升序", Command = SetSortStatusAscCommand },
            new() { Text = "状态 · 降序", Command = SetSortStatusDescCommand },
        };
        return new ReadOnlyObservableCollection<OperationBarMenuItem>(items);
    }

    [RelayCommand] private void SetStatusFilterAll() => StatusFilter = null;
    [RelayCommand] private void SetStatusFilterPending() => StatusFilter = IdeaStatuses.Pending;
    [RelayCommand] private void SetStatusFilterApproved() => StatusFilter = IdeaStatuses.Approved;
    [RelayCommand] private void SetStatusFilterShelved() => StatusFilter = IdeaStatuses.Shelved;
    [RelayCommand] private void SetPriorityFilterAll() => PriorityFilter = null;
    [RelayCommand] private void SetPriorityFilterP0() => PriorityFilter = IdeaPriorities.P0;
    [RelayCommand] private void SetPriorityFilterP1() => PriorityFilter = IdeaPriorities.P1;
    [RelayCommand] private void SetPriorityFilterP2() => PriorityFilter = IdeaPriorities.P2;
    [RelayCommand] private void SetPriorityFilterP3() => PriorityFilter = IdeaPriorities.P3;
    [RelayCommand] private void SetSortUpdatedDesc() { SortField = IdeaSortField.UpdatedAt; SortDescending = true; }
    [RelayCommand] private void SetSortUpdatedAsc() { SortField = IdeaSortField.UpdatedAt; SortDescending = false; }
    [RelayCommand] private void SetSortTitleAsc() { SortField = IdeaSortField.Title; SortDescending = false; }
    [RelayCommand] private void SetSortTitleDesc() { SortField = IdeaSortField.Title; SortDescending = true; }
    [RelayCommand] private void SetSortStatusAsc() { SortField = IdeaSortField.Status; SortDescending = false; }
    [RelayCommand] private void SetSortStatusDesc() { SortField = IdeaSortField.Status; SortDescending = true; }

    public async Task JumpToEntityFromSearchAsync(string ideaId)
    {
        SearchQuery = "";
        StatusFilter = null;
        PriorityFilter = null;
        ClearIdeaSearchHighlights();
        await RefreshAsync().ConfigureAwait(true);
        var row = Ideas.FirstOrDefault(x => x.Id == ideaId);
        if (row is null)
        {
            return;
        }

        row.IsSearchHighlight = true;
        SelectedIdeaRow = row;
        ScheduleIdeaSearchHighlightClear();
    }

    private void ClearIdeaSearchHighlights()
    {
        foreach (var i in Ideas)
        {
            i.IsSearchHighlight = false;
        }
    }

    private void ScheduleIdeaSearchHighlightClear()
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
        _searchHighlightClearTimer.Tick += OnIdeaSearchHighlightClearTick;
        _searchHighlightClearTimer.Start();
    }

    private void OnIdeaSearchHighlightClearTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnIdeaSearchHighlightClearTick;
        ClearIdeaSearchHighlights();
    }

    private static string Now() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}

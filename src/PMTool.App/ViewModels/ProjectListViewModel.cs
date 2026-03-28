using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PMTool.App.Diagnostics;
using PMTool.App.Models;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;
using PMTool.Core.Validation;

namespace PMTool.App.ViewModels;

public partial class ProjectListViewModel(
    IProjectRepository projectRepository,
    IProjectDeletionGuard projectDeletionGuard) : ObservableObject, IOperationBarViewModel
{
    private DispatcherQueueTimer? _searchHighlightClearTimer;
    private DispatcherQueueTimer? _searchDebounceTimer;

    private ReadOnlyObservableCollection<OperationBarMenuItem>? _filterMenuItems;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _sortMenuItems;

    public string SearchPlaceholderText => "搜索项目名称或描述…";

    public bool IsOperationBarInteractive => true;

    public string PrimaryActionLabel => "新建项目";

    public IRelayCommand? PrimaryActionCommand => RequestNewProjectUiCommand;

    public ReadOnlyObservableCollection<OperationBarMenuItem> FilterMenuItems =>
        _filterMenuItems ??= BuildFilterMenuItems();

    public ReadOnlyObservableCollection<OperationBarMenuItem> SortMenuItems =>
        _sortMenuItems ??= BuildSortMenuItems();

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildFilterMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "全部", Command = SetFilterAllCommand },
            new() { Text = "进行中", Command = SetFilterInProgressCommand },
            new() { Text = "已归档", Command = SetFilterArchivedCommand },
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

    public ObservableCollection<ProjectRowViewModel> Projects { get; } = [];

    [ObservableProperty]
    private ProjectRowViewModel? _selectedProject;

    [ObservableProperty]
    private string _searchQuery = "";

    /// <summary>null = 全部；否则为 <see cref="ProjectStatuses"/> 值。</summary>
    [ObservableProperty]
    private string? _statusFilter;

    [ObservableProperty]
    private ProjectSortField _sortField = ProjectSortField.UpdatedAt;

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    private string _errorBanner = "";

    public Visibility ErrorBannerVisibility =>
        string.IsNullOrEmpty(ErrorBanner) ? Visibility.Collapsed : Visibility.Visible;

    public string FilterButtonText => StatusFilter switch
    {
        null => "筛选：全部",
        ProjectStatuses.InProgress => "筛选：进行中",
        ProjectStatuses.Archived => "筛选：已归档",
        _ => "筛选",
    };

    public string SortButtonText =>
        $"{SortFieldToLabel(SortField)} · {(SortDescending ? "降序" : "升序")}";

    public bool IsDetailArchived => SelectedProject?.Status == ProjectStatuses.Archived;

    public bool CanEditDetail => SelectedProject is not null && !IsDetailArchived;

    public Microsoft.UI.Xaml.Visibility DetailPanelVisibility =>
        SelectedProject is null ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    public bool ShowGlobalEmpty => Projects.Count == 0 && string.IsNullOrWhiteSpace(SearchQuery) && StatusFilter is null;

    public bool ShowNoMatch => Projects.Count == 0 && (!string.IsNullOrWhiteSpace(SearchQuery) || StatusFilter is not null);

    public event EventHandler? NewProjectUiRequested;

    public event EventHandler? EditProjectUiRequested;

    partial void OnSelectedProjectChanged(ProjectRowViewModel? value)
    {
        OnPropertyChanged(nameof(DetailPanelVisibility));
        OnPropertyChanged(nameof(DetailTitle));
        OnPropertyChanged(nameof(DetailStatus));
        OnPropertyChanged(nameof(DetailBody));
        OnPropertyChanged(nameof(DetailStats));
        OnPropertyChanged(nameof(IsDetailArchived));
        OnPropertyChanged(nameof(CanEditDetail));
        OnPropertyChanged(nameof(ShowRestoreInsteadOfArchive));
        OnPropertyChanged(nameof(ArchiveButtonVisibility));
        OnPropertyChanged(nameof(RestoreButtonVisibility));
    }

    partial void OnErrorBannerChanged(string value)
    {
        OnPropertyChanged(nameof(ErrorBannerVisibility));
    }

    partial void OnSearchQueryChanged(string value)
    {
        ScheduleSearchRefresh();
    }

    partial void OnStatusFilterChanged(string? value)
    {
        OnPropertyChanged(nameof(FilterButtonText));
        _ = RefreshAsync();
    }

    partial void OnSortFieldChanged(ProjectSortField value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = RefreshAsync();
    }

    partial void OnSortDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = RefreshAsync();
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
        await RefreshAsync().ConfigureAwait(true);
    }

    public string DetailTitle => SelectedProject?.Name ?? string.Empty;

    public string DetailStatus => SelectedProject?.Status ?? string.Empty;

    public string DetailBody => SelectedProject?.Description ?? string.Empty;

    public string DetailStats => SelectedProject?.SummaryLine ?? string.Empty;

    public bool ShowRestoreInsteadOfArchive => SelectedProject?.Status == ProjectStatuses.Archived;

    public Visibility ArchiveButtonVisibility =>
        SelectedProject is not null && SelectedProject.Status != ProjectStatuses.Archived
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility RestoreButtonVisibility =>
        SelectedProject is not null && SelectedProject.Status == ProjectStatuses.Archived
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => ShowGlobalEmpty ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoMatchVisibility => ShowNoMatch ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ListVisibility => Projects.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        ErrorBanner = "";
        try
        {
            var q = new ProjectListQuery(
                StatusFilter,
                string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim(),
                SortField,
                SortDescending);
            var list = await projectRepository.ListAsync(q).ConfigureAwait(true);
            Projects.Clear();
            foreach (var item in list)
            {
                Projects.Add(ProjectRowViewModel.FromItem(item));
            }

            OnPropertyChanged(nameof(ShowGlobalEmpty));
            OnPropertyChanged(nameof(ShowNoMatch));
            OnPropertyChanged(nameof(EmptyStateVisibility));
            OnPropertyChanged(nameof(NoMatchVisibility));
            OnPropertyChanged(nameof(ListVisibility));
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private void SelectProject(ProjectRowViewModel? row)
    {
        SelectedProject = row;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedProject = null;
    }

    [RelayCommand]
    private void RequestNewProjectUi()
    {
        // #region agent log
        DebugAgentLog.Write("A", "ProjectListViewModel.RequestNewProjectUi", "command invoked", new Dictionary<string, string> { ["thread"] = Environment.CurrentManagedThreadId.ToString() });
        // #endregion
        NewProjectUiRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RequestEditProjectUi()
    {
        if (SelectedProject is null || SelectedProject.Status == ProjectStatuses.Archived)
        {
            return;
        }

        EditProjectUiRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private Task SetFilterAllAsync()
    {
        StatusFilter = null;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task SetFilterInProgressAsync()
    {
        StatusFilter = ProjectStatuses.InProgress;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task SetFilterArchivedAsync()
    {
        StatusFilter = ProjectStatuses.Archived;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task SetSortUpdatedDescAsync()
    {
        SortField = ProjectSortField.UpdatedAt;
        SortDescending = true;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task SetSortNameAscAsync()
    {
        SortField = ProjectSortField.Name;
        SortDescending = false;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task SetSortCreatedDescAsync()
    {
        SortField = ProjectSortField.CreatedAt;
        SortDescending = true;
        return RefreshAsync();
    }

    [RelayCommand]
    private async Task ArchiveSelectedAsync()
    {
        if (SelectedProject is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            var p = await projectRepository.GetByIdAsync(SelectedProject.Id).ConfigureAwait(true);
            if (p is null)
            {
                return;
            }

            if (p.Status == ProjectStatuses.Archived)
            {
                return;
            }

            var now = Now();
            await projectRepository.UpdateCoreAsync(new Project
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Status = ProjectStatuses.Archived,
                Category = p.Category,
                CreatedAt = p.CreatedAt,
                UpdatedAt = now,
                IsDeleted = p.IsDeleted,
                RowVersion = p.RowVersion,
            }).ConfigureAwait(true);

            await RefreshAsync().ConfigureAwait(true);
            ReselectById(p.Id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RestoreSelectedAsync()
    {
        if (SelectedProject is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            var p = await projectRepository.GetByIdAsync(SelectedProject.Id).ConfigureAwait(true);
            if (p is null)
            {
                return;
            }

            if (p.Status != ProjectStatuses.Archived)
            {
                return;
            }

            var name = ProjectFieldValidator.ValidateName(p.Name);
            if (await projectRepository.ExistsNameInStatusAsync(name, ProjectStatuses.InProgress, p.Id).ConfigureAwait(true))
            {
                ErrorBanner = "已存在同名的进行中项目，无法恢复。请先重命名归档项目或处理名称冲突。";
                return;
            }

            var now = Now();
            await projectRepository.UpdateCoreAsync(new Project
            {
                Id = p.Id,
                Name = p.Name,
                Description = ProjectFieldValidator.ValidateDescription(p.Description),
                Status = ProjectStatuses.InProgress,
                Category = p.Category,
                CreatedAt = p.CreatedAt,
                UpdatedAt = now,
                IsDeleted = p.IsDeleted,
                RowVersion = p.RowVersion,
            }).ConfigureAwait(true);

            await RefreshAsync().ConfigureAwait(true);
            ReselectById(p.Id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedProject is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            if (await projectDeletionGuard.HasBlockingAssociationsAsync(SelectedProject.Id).ConfigureAwait(true))
            {
                ErrorBanner = "该项目有关联内容，请先删除关联内容或归档项目。";
                return;
            }

            await projectRepository.SoftDeleteAsync(SelectedProject.Id).ConfigureAwait(true);
            SelectedProject = null;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    public async Task CreateProjectAsync(string name, string description)
    {
        var n = ProjectFieldValidator.ValidateName(name);
        var d = ProjectFieldValidator.ValidateDescription(description);
        if (await projectRepository.ExistsNameInStatusAsync(n, ProjectStatuses.InProgress, null).ConfigureAwait(true))
        {
            throw new InvalidOperationException("该项目名称已存在，请修改。");
        }

        var now = Now();
        var p = new Project
        {
            Id = Guid.NewGuid().ToString("D"),
            Name = n,
            Description = d,
            Status = ProjectStatuses.InProgress,
            Category = null,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            RowVersion = 1,
        };

        await projectRepository.InsertAsync(p).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        ReselectById(p.Id);
    }

    public async Task UpdateProjectAsync(string id, string name, string description)
    {
        var existing = await projectRepository.GetByIdAsync(id).ConfigureAwait(true)
            ?? throw new InvalidOperationException("项目不存在。");
        if (existing.Status == ProjectStatuses.Archived)
        {
            throw new InvalidOperationException("已归档项目不可编辑。");
        }

        var n = ProjectFieldValidator.ValidateName(name);
        var d = ProjectFieldValidator.ValidateDescription(description);
        if (await projectRepository.ExistsNameInStatusAsync(n, existing.Status, id).ConfigureAwait(true))
        {
            throw new InvalidOperationException("该项目名称在当前状态下已存在，请修改。");
        }

        var now = Now();
        await projectRepository.UpdateCoreAsync(new Project
        {
            Id = existing.Id,
            Name = n,
            Description = d,
            Status = existing.Status,
            Category = existing.Category,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = now,
            IsDeleted = existing.IsDeleted,
            RowVersion = existing.RowVersion,
        }).ConfigureAwait(true);

        await RefreshAsync().ConfigureAwait(true);
        ReselectById(id);
    }

    private void ReselectById(string id)
    {
        SelectedProject = Projects.FirstOrDefault(x => x.Id == id);
    }

    public async Task JumpToEntityFromSearchAsync(string projectId)
    {
        SearchQuery = "";
        StatusFilter = null;
        ClearProjectSearchHighlights();
        await RefreshAsync().ConfigureAwait(true);
        var row = Projects.FirstOrDefault(x => x.Id == projectId);
        if (row is null)
        {
            return;
        }

        row.IsSearchHighlight = true;
        SelectedProject = row;
        ScheduleProjectSearchHighlightClear();
    }

    private void ClearProjectSearchHighlights()
    {
        foreach (var p in Projects)
        {
            p.IsSearchHighlight = false;
        }
    }

    private void ScheduleProjectSearchHighlightClear()
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
        _searchHighlightClearTimer.Tick += OnProjectSearchHighlightClearTick;
        _searchHighlightClearTimer.Start();
    }

    private void OnProjectSearchHighlightClearTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnProjectSearchHighlightClearTick;
        ClearProjectSearchHighlights();
    }

    private static string Now() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string SortFieldToLabel(ProjectSortField f) => f switch
    {
        ProjectSortField.Name => "名称",
        ProjectSortField.CreatedAt => "创建时间",
        _ => "更新时间",
    };
}

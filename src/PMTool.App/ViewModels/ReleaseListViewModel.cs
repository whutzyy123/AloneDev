using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PMTool.App.Models;
using MiniExcelLibs;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;
using PMTool.Core.Validation;

namespace PMTool.App.ViewModels;

public partial class ReleaseListViewModel(
    IReleaseRepository releaseRepository,
    IProjectRepository projectRepository,
    IFeatureRepository featureRepository,
    ITaskRepository taskRepository,
    ICurrentAccountContext accountContext) : ObservableObject, IOperationBarViewModel
{
    private DispatcherQueueTimer? _searchDebounceTimer;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _filterMenuItems;
    private ReadOnlyObservableCollection<OperationBarMenuItem>? _sortMenuItems;

    public string SearchPlaceholderText => "搜索版本名称或描述…";

    public bool IsOperationBarInteractive => SelectedProjectId is { Length: > 0 };

    public string PrimaryActionLabel => "新建版本";

    public IRelayCommand? PrimaryActionCommand => RequestNewReleaseUiCommand;

    public ReadOnlyObservableCollection<OperationBarMenuItem> FilterMenuItems =>
        _filterMenuItems ??= BuildFilterMenuItems();

    public ReadOnlyObservableCollection<OperationBarMenuItem> SortMenuItems =>
        _sortMenuItems ??= BuildSortMenuItems();

    public ObservableCollection<ProjectPickerItem> ProjectOptions { get; } = [];

    public ObservableCollection<ReleaseRowViewModel> Releases { get; } = [];

    public ObservableCollection<ReleaseRelationItemViewModel> DetailRelations { get; } = [];

    [ObservableProperty]
    private string? _selectedProjectId;

    [ObservableProperty]
    private ReleaseRowViewModel? _selectedRelease;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string? _statusFilter;

    [ObservableProperty]
    private ReleaseSortField _sortField = ReleaseSortField.UpdatedAt;

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    private string _errorBanner = "";

    [ObservableProperty]
    private string _detailProgressText = string.Empty;

    public string FilterButtonText => StatusFilter switch
    {
        null => "筛选：全部",
        ReleaseStatuses.NotStarted => "筛选：未开始",
        ReleaseStatuses.InProgress => "筛选：进行中",
        ReleaseStatuses.Ended => "筛选：已结束",
        ReleaseStatuses.Cancelled => "筛选：已取消",
        _ => "筛选",
    };

    public string SortButtonText =>
        $"{SortFieldToLabel(SortField)} · {(SortDescending ? "降序" : "升序")}";

    public Visibility ErrorBannerVisibility =>
        string.IsNullOrEmpty(ErrorBanner) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DetailPanelVisibility =>
        SelectedRelease is null ? Visibility.Collapsed : Visibility.Visible;

    public bool ShowProjectsMissing => ProjectOptions.Count == 0;

    public bool ShowSelectProject => ProjectOptions.Count > 0 && string.IsNullOrEmpty(SelectedProjectId);

    public bool ShowGlobalEmpty =>
        !string.IsNullOrEmpty(SelectedProjectId) && Releases.Count == 0
        && string.IsNullOrWhiteSpace(SearchQuery) && StatusFilter is null;

    public bool ShowNoMatch =>
        !string.IsNullOrEmpty(SelectedProjectId) && Releases.Count == 0
        && (!string.IsNullOrWhiteSpace(SearchQuery) || StatusFilter is not null);

    public Visibility ListVisibility => Releases.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyProjectsVisibility => ShowProjectsMissing ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectProjectVisibility => ShowSelectProject ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GlobalEmptyVisibility => ShowGlobalEmpty ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoMatchVisibility => ShowNoMatch ? Visibility.Visible : Visibility.Collapsed;

    public string DetailTitle => SelectedRelease?.Name ?? string.Empty;

    public string DetailMeta =>
        SelectedRelease is { } r
            ? $"{r.Status} · {r.StartAt} ~ {r.EndAt} · 更新 {r.UpdatedAt}"
            : string.Empty;

    public bool CanEditSelected =>
        SelectedRelease is not null && GetDetailStatus() == ReleaseStatuses.NotStarted;

    public bool CanDeleteSelected => CanEditSelected;

    public bool CanStartSelected =>
        SelectedRelease is not null && GetDetailStatus() == ReleaseStatuses.NotStarted;

    public bool CanFinishSelected =>
        SelectedRelease is not null && GetDetailStatus() == ReleaseStatuses.InProgress;

    public bool CanAddRelation =>
        SelectedRelease is not null
        && (GetDetailStatus() == ReleaseStatuses.NotStarted || GetDetailStatus() == ReleaseStatuses.InProgress);

    public event EventHandler? NewReleaseUiRequested;

    public event EventHandler? EditReleaseUiRequested;

    public event EventHandler? AddRelationUiRequested;

    partial void OnSelectedProjectIdChanged(string? value)
    {
        OnPropertyChanged(nameof(IsOperationBarInteractive));
        OnPropertyChanged(nameof(ShowSelectProject));
        OnPropertyChanged(nameof(SelectProjectVisibility));
        _ = RefreshReleasesOnlyAsync();
    }

    partial void OnSelectedReleaseChanged(ReleaseRowViewModel? value)
    {
        DetailRelations.Clear();
        DetailProgressText = string.Empty;
        OnPropertyChanged(nameof(DetailPanelVisibility));
        OnPropertyChanged(nameof(DetailTitle));
        OnPropertyChanged(nameof(DetailMeta));
        OnPropertyChanged(nameof(CanEditSelected));
        OnPropertyChanged(nameof(CanDeleteSelected));
        OnPropertyChanged(nameof(CanStartSelected));
        OnPropertyChanged(nameof(CanFinishSelected));
        OnPropertyChanged(nameof(CanAddRelation));
        if (value is not null)
        {
            _ = LoadDetailAsync(value.Id);
        }
        else
        {
            NotifyDetailCommands();
        }
    }

    partial void OnErrorBannerChanged(string value) => OnPropertyChanged(nameof(ErrorBannerVisibility));

    partial void OnStatusFilterChanged(string? value)
    {
        OnPropertyChanged(nameof(FilterButtonText));
        _ = RefreshReleasesOnlyAsync();
    }

    partial void OnSortFieldChanged(ReleaseSortField value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = RefreshReleasesOnlyAsync();
    }

    partial void OnSortDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortButtonText));
        _ = RefreshReleasesOnlyAsync();
    }

    partial void OnSearchQueryChanged(string value) => ScheduleSearchRefresh();

    private string? GetDetailStatus() => SelectedRelease?.Status;

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
        await RefreshReleasesOnlyAsync().ConfigureAwait(true);
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildFilterMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "全部", Command = SetFilterAllCommand },
            new() { Text = "未开始", Command = SetFilterNotStartedCommand },
            new() { Text = "进行中", Command = SetFilterInProgressCommand },
            new() { Text = "已结束", Command = SetFilterEndedCommand },
            new() { Text = "已取消", Command = SetFilterCancelledCommand },
        };
        return new ReadOnlyObservableCollection<OperationBarMenuItem>(items);
    }

    private ReadOnlyObservableCollection<OperationBarMenuItem> BuildSortMenuItems()
    {
        var items = new ObservableCollection<OperationBarMenuItem>
        {
            new() { Text = "更新时间 · 降序", Command = SetSortUpdatedDescCommand },
            new() { Text = "名称 · 升序", Command = SetSortNameAscCommand },
            new() { Text = "开始时间 · 降序", Command = SetSortStartDescCommand },
        };
        return new ReadOnlyObservableCollection<OperationBarMenuItem>(items);
    }

    private static string SortFieldToLabel(ReleaseSortField f) => f switch
    {
        ReleaseSortField.Name => "名称",
        ReleaseSortField.StartAt => "开始时间",
        _ => "更新时间",
    };

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

            OnPropertyChanged(nameof(IsOperationBarInteractive));
            OnPropertyChanged(nameof(ShowProjectsMissing));
            OnPropertyChanged(nameof(ShowSelectProject));
            OnPropertyChanged(nameof(EmptyProjectsVisibility));
            OnPropertyChanged(nameof(SelectProjectVisibility));
            await RefreshReleasesOnlyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private async Task RefreshReleasesOnlyAsync()
    {
        ErrorBanner = "";
        try
        {
            var prevId = SelectedRelease?.Id;
            if (string.IsNullOrEmpty(SelectedProjectId))
            {
                Releases.Clear();
                SelectedRelease = null;
            }
            else
            {
                var q = new ReleaseListQuery
                {
                    ProjectId = SelectedProjectId,
                    SearchText = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim(),
                    StatusFilter = StatusFilter,
                    SortField = SortField,
                    SortDescending = SortDescending,
                };
                var list = await releaseRepository.ListAsync(q).ConfigureAwait(true);
                var ids = list.Select(x => x.Id).ToList();
                var progressMap = await releaseRepository.GetProgressBatchAsync(ids).ConfigureAwait(true);
                Releases.Clear();
                foreach (var r in list)
                {
                    var p = progressMap.TryGetValue(r.Id, out var st) ? st : new ReleaseProgressStats(0, 0, 0, 0, 0);
                    Releases.Add(ReleaseRowViewModel.FromRelease(r, p));
                }

                if (prevId is not null)
                {
                    SelectedRelease = Releases.FirstOrDefault(x => x.Id == prevId);
                }
            }

            OnPropertyChanged(nameof(ShowGlobalEmpty));
            OnPropertyChanged(nameof(ShowNoMatch));
            OnPropertyChanged(nameof(GlobalEmptyVisibility));
            OnPropertyChanged(nameof(NoMatchVisibility));
            OnPropertyChanged(nameof(ListVisibility));
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private async Task LoadDetailAsync(string releaseId)
    {
        try
        {
            var prog = await releaseRepository.GetProgressAsync(releaseId).ConfigureAwait(true);
            DetailProgressText =
                $"特性 完成 {prog.CompletedFeatures}/{prog.TotalFeatures} · 任务 完成 {prog.CompletedTasks}/{prog.TotalTasks} · 综合进度 {prog.Percent:0.0}%（PRD 6.5.2）";

            var rels = await releaseRepository.ListRelationsAsync(releaseId).ConfigureAwait(true);
            DetailRelations.Clear();
            foreach (var row in rels)
            {
                DetailRelations.Add(ReleaseRelationItemViewModel.FromRow(row));
            }

            NotifyDetailCommands();
        }
        catch
        {
            DetailProgressText = "进度加载失败";
        }
    }

    private void NotifyDetailCommands()
    {
        OnPropertyChanged(nameof(CanEditSelected));
        OnPropertyChanged(nameof(CanDeleteSelected));
        OnPropertyChanged(nameof(CanStartSelected));
        OnPropertyChanged(nameof(CanFinishSelected));
        OnPropertyChanged(nameof(CanAddRelation));
    }

    [RelayCommand]
    private void SelectReleaseRow(ReleaseRowViewModel? row) => SelectedRelease = row;

    [RelayCommand]
    private void ClearReleaseSelection() => SelectedRelease = null;

    [RelayCommand]
    private Task SetFilterAllAsync()
    {
        StatusFilter = null;
        return RefreshReleasesOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterNotStartedAsync()
    {
        StatusFilter = ReleaseStatuses.NotStarted;
        return RefreshReleasesOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterInProgressAsync()
    {
        StatusFilter = ReleaseStatuses.InProgress;
        return RefreshReleasesOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterEndedAsync()
    {
        StatusFilter = ReleaseStatuses.Ended;
        return RefreshReleasesOnlyAsync();
    }

    [RelayCommand]
    private Task SetFilterCancelledAsync()
    {
        StatusFilter = ReleaseStatuses.Cancelled;
        return RefreshReleasesOnlyAsync();
    }

    [RelayCommand]
    private Task SetSortUpdatedDescAsync()
    {
        SortField = ReleaseSortField.UpdatedAt;
        SortDescending = true;
        return RefreshReleasesOnlyAsync();
    }

    [RelayCommand]
    private Task SetSortNameAscAsync()
    {
        SortField = ReleaseSortField.Name;
        SortDescending = false;
        return RefreshReleasesOnlyAsync();
    }

    [RelayCommand]
    private Task SetSortStartDescAsync()
    {
        SortField = ReleaseSortField.StartAt;
        SortDescending = true;
        return RefreshReleasesOnlyAsync();
    }

    [RelayCommand]
    private void RequestNewReleaseUi()
    {
        if (string.IsNullOrEmpty(SelectedProjectId))
        {
            ErrorBanner = "请先选择项目。";
            return;
        }

        NewReleaseUiRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RequestEditReleaseUi()
    {
        if (!CanEditSelected || SelectedRelease is null)
        {
            return;
        }

        EditReleaseUiRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ExportReleaseReportAsync()
    {
        if (SelectedRelease is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            var rid = SelectedRelease.Id;
            var entity = await releaseRepository.GetByIdAsync(rid).ConfigureAwait(true)
                ?? throw new InvalidOperationException("版本不存在。");
            var rels = await releaseRepository.ListRelationsAsync(rid).ConfigureAwait(true);
            var prog = await releaseRepository.GetProgressAsync(rid).ConfigureAwait(true);
            var dir = Path.Combine(accountContext.GetAccountDirectoryPath(), "Report");
            _ = Directory.CreateDirectory(dir);
            var fn = $"{SanitizeFileName(entity.Name)}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
            var path = Path.Combine(dir, fn);
            var summary = new[]
            {
                new
                {
                    名称 = entity.Name,
                    状态 = entity.Status,
                    开始 = entity.StartAt,
                    结束 = entity.EndAt,
                    描述 = entity.Description,
                    特性完成 = $"{prog.CompletedFeatures}/{prog.TotalFeatures}",
                    任务完成 = $"{prog.CompletedTasks}/{prog.TotalTasks}",
                    进度百分比 = Math.Round(prog.Percent, 1),
                },
            };
            var relRows = rels.Select(r => new { 类型 = r.TargetType, 标识 = r.TargetId, 名称 = r.DisplayName, }).ToList();
            var sheets = new Dictionary<string, object>
            {
                ["概要"] = summary,
                ["关联"] = relRows,
            };
            await MiniExcel.SaveAsAsync(path, sheets).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "release" : s[..Math.Min(s.Length, 80)];
    }

    [RelayCommand]
    private void RequestAddRelationUi()
    {
        if (!CanAddRelation)
        {
            return;
        }

        AddRelationUiRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task CreateReleaseAsync(string name, string description, string startAt, string endAt)
    {
        if (string.IsNullOrEmpty(SelectedProjectId))
        {
            throw new InvalidOperationException("未选择项目。");
        }

        var n = ReleaseFieldValidator.ValidateName(name);
        var d = ReleaseFieldValidator.ValidateDescription(description);
        var s = ReleaseFieldValidator.ValidateRequiredDateText(startAt, "开始时间");
        var e = ReleaseFieldValidator.ValidateRequiredDateText(endAt, "结束时间");
        if (!TryValidateReleaseDateOrder(s, e, out var dateErr))
        {
            throw new InvalidOperationException(dateErr);
        }

        var now = Now();
        var r = new Release
        {
            Id = Guid.NewGuid().ToString("D"),
            ProjectId = SelectedProjectId,
            Name = n,
            Description = d,
            StartAt = s,
            EndAt = e,
            Status = ReleaseStatuses.NotStarted,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            RowVersion = 1,
        };

        await releaseRepository.InsertAsync(r).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        SelectedRelease = Releases.FirstOrDefault(x => x.Id == r.Id);
    }

    public async Task UpdateReleaseAsync(string id, string name, string description, string startAt, string endAt)
    {
        var existing = await releaseRepository.GetByIdAsync(id).ConfigureAwait(true)
            ?? throw new InvalidOperationException("版本不存在。");
        if (existing.Status != ReleaseStatuses.NotStarted)
        {
            throw new InvalidOperationException("仅未开始版本可编辑。");
        }

        var n = ReleaseFieldValidator.ValidateName(name);
        var d = ReleaseFieldValidator.ValidateDescription(description);
        var s = ReleaseFieldValidator.ValidateRequiredDateText(startAt, "开始时间");
        var e = ReleaseFieldValidator.ValidateRequiredDateText(endAt, "结束时间");
        if (!TryValidateReleaseDateOrder(s, e, out var dateErr))
        {
            throw new InvalidOperationException(dateErr);
        }

        var now = Now();
        await releaseRepository.UpdateAsync(new Release
        {
            Id = existing.Id,
            ProjectId = existing.ProjectId,
            Name = n,
            Description = d,
            StartAt = s,
            EndAt = e,
            Status = existing.Status,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = now,
            IsDeleted = existing.IsDeleted,
            RowVersion = existing.RowVersion,
        }).ConfigureAwait(true);

        await RefreshAsync().ConfigureAwait(true);
        SelectedRelease = Releases.FirstOrDefault(x => x.Id == id);
    }

    [RelayCommand]
    private async Task StartReleaseAsync()
    {
        if (SelectedRelease is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            var existing = await releaseRepository.GetByIdAsync(SelectedRelease.Id).ConfigureAwait(true)
                ?? throw new InvalidOperationException("版本不存在。");
            var now = Now();
            await releaseRepository.UpdateAsync(new Release
            {
                Id = existing.Id,
                ProjectId = existing.ProjectId,
                Name = existing.Name,
                Description = existing.Description,
                StartAt = existing.StartAt,
                EndAt = existing.EndAt,
                Status = ReleaseStatuses.InProgress,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = now,
                IsDeleted = existing.IsDeleted,
                RowVersion = existing.RowVersion,
            }).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SelectedRelease = Releases.FirstOrDefault(x => x.Id == existing.Id);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private async Task MarkReleaseEndedAsync() => await ApplyTerminalStatusCoreAsync(ReleaseStatuses.Ended).ConfigureAwait(true);

    [RelayCommand]
    private async Task MarkReleaseCancelledAsync() => await ApplyTerminalStatusCoreAsync(ReleaseStatuses.Cancelled).ConfigureAwait(true);

    private async Task ApplyTerminalStatusCoreAsync(string targetStatus)
    {
        if (SelectedRelease is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            var rid = SelectedRelease.Id;
            var existing = await releaseRepository.GetByIdAsync(rid).ConfigureAwait(true)
                ?? throw new InvalidOperationException("版本不存在。");
            var now = Now();
            await releaseRepository.UpdateAsync(new Release
            {
                Id = existing.Id,
                ProjectId = existing.ProjectId,
                Name = existing.Name,
                Description = existing.Description,
                StartAt = existing.StartAt,
                EndAt = existing.EndAt,
                Status = targetStatus,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = now,
                IsDeleted = existing.IsDeleted,
                RowVersion = existing.RowVersion,
            }).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SelectedRelease = Releases.FirstOrDefault(x => x.Id == rid);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedRelease is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            await releaseRepository.SoftDeleteAsync(SelectedRelease.Id).ConfigureAwait(true);
            SelectedRelease = null;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RemoveRelationAsync(ReleaseRelationItemViewModel? item)
    {
        if (SelectedRelease is null || item is null)
        {
            return;
        }

        try
        {
            ErrorBanner = "";
            var rid = SelectedRelease.Id;
            await releaseRepository.RemoveRelationAsync(rid, item.TargetType, item.TargetId)
                .ConfigureAwait(true);
            await LoadDetailAsync(rid).ConfigureAwait(true);
            await RefreshReleasesOnlyAsync().ConfigureAwait(true);
            SelectedRelease = Releases.FirstOrDefault(x => x.Id == rid);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    public async Task AddRelationsAsync(IEnumerable<(string TargetType, string TargetId)> pairs)
    {
        if (SelectedRelease is null)
        {
            return;
        }

        ErrorBanner = "";
        foreach (var (tt, tid) in pairs)
        {
            try
            {
                await releaseRepository.AddRelationAsync(SelectedRelease.Id, tt, tid).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ErrorBanner = ex.Message;
                break;
            }
        }

        await LoadDetailAsync(SelectedRelease.Id).ConfigureAwait(true);
        await RefreshReleasesOnlyAsync().ConfigureAwait(true);
        var id = SelectedRelease.Id;
        SelectedRelease = Releases.FirstOrDefault(x => x.Id == id);
    }

    public Task<Release?> GetReleaseEntityAsync(string id) => releaseRepository.GetByIdAsync(id);

    public async Task<IReadOnlyList<Feature>> GetProjectFeaturesForPickerAsync()
    {
        if (string.IsNullOrEmpty(SelectedProjectId))
        {
            return [];
        }

        var q = new FeatureListQuery
        {
            ProjectId = SelectedProjectId,
            SearchText = null,
            StatusFilter = null,
            SortField = FeatureSortField.Name,
            SortDescending = false,
        };
        return await featureRepository.ListAsync(q).ConfigureAwait(true);
    }

    public async Task<IReadOnlyList<PmTask>> GetProjectTasksForPickerAsync()
    {
        if (string.IsNullOrEmpty(SelectedProjectId))
        {
            return [];
        }

        return await taskRepository.ListByProjectAsync(SelectedProjectId).ConfigureAwait(true);
    }

    private static bool TryValidateReleaseDateOrder(string startAt, string endAt, out string? error)
    {
        error = null;
        if (!TryParseReleaseInstant(startAt, out var ds) || !TryParseReleaseInstant(endAt, out var de))
        {
            error = "开始时间或结束时间格式无效，请使用可解析的日期时间。";
            return false;
        }

        if (ds > de)
        {
            error = "开始时间不可晚于结束时间。";
            return false;
        }

        return true;
    }

    private static bool TryParseReleaseInstant(string text, out DateTime utc)
    {
        if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out utc))
        {
            return true;
        }

        return DateTime.TryParse(
            text,
            CultureInfo.GetCultureInfo("zh-CN"),
            DateTimeStyles.AssumeLocal,
            out utc);
    }

    private static string Now() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}

namespace PMTool.Core.Models;

public sealed class TaskListQuery
{
    /// <summary>按项目列举（含所有模块下及无模块的任务）。与 <see cref="FeatureId"/> 二选一。</summary>
    public string? ProjectId { get; init; }

    /// <summary>按单一模块列举。非空时优先于此，忽略 <see cref="ProjectId"/>。</summary>
    public string? FeatureId { get; init; }

    public string? SearchText { get; init; }
    public string? StatusFilter { get; init; }
    public TaskSortMode SortMode { get; init; } = TaskSortMode.ManualOrder;
    public bool SortDescending { get; init; }
}

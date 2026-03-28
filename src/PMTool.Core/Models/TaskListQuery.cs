namespace PMTool.Core.Models;

public sealed class TaskListQuery
{
    public required string FeatureId { get; init; }
    public string? SearchText { get; init; }
    public string? StatusFilter { get; init; }
    public TaskSortMode SortMode { get; init; } = TaskSortMode.ManualOrder;
    public bool SortDescending { get; init; }
}

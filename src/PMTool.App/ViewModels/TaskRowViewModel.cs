using PMTool.Core;
using PMTool.Core.Models;

namespace PMTool.App.ViewModels;

public sealed partial class TaskRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSearchHighlight;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string TaskType { get; init; }
    public required string Status { get; init; }
    public string SeverityDisplay { get; init; } = "—";
    public double EstimatedHours { get; init; }
    public required string UpdatedAt { get; init; }

    public static TaskRowViewModel FromTask(PmTask t) =>
        new()
        {
            Id = t.Id,
            Name = t.Name,
            TaskType = t.TaskType,
            Status = t.Status,
            SeverityDisplay = t.TaskType == TaskTypes.Bug && t.Severity is { Length: > 0 } s ? s : "—",
            EstimatedHours = t.EstimatedHours,
            UpdatedAt = t.UpdatedAt,
        };
}

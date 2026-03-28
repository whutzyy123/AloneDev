using PMTool.Core.Models;

namespace PMTool.App.ViewModels;

public sealed class ReleaseRowViewModel
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string StartAt { get; init; }

    public required string EndAt { get; init; }

    public required string Status { get; init; }

    public required string UpdatedAt { get; init; }

    public int LinkedFeatures { get; init; }

    public int LinkedTasks { get; init; }

    public double ProgressPercent { get; init; }

    public string TimeRangeText => $"{StartAt} ~ {EndAt}";

    public string ProgressLabel => $"{ProgressPercent:0.0}%";

    public static ReleaseRowViewModel FromRelease(Release r, ReleaseProgressStats progress) =>
        new()
        {
            Id = r.Id,
            Name = r.Name,
            StartAt = r.StartAt,
            EndAt = r.EndAt,
            Status = r.Status,
            UpdatedAt = r.UpdatedAt,
            LinkedFeatures = progress.TotalFeatures,
            LinkedTasks = progress.TotalTasks,
            ProgressPercent = progress.Percent,
        };
}

using PMTool.Core.Models;

namespace PMTool.App.ViewModels;

public sealed class ReleaseRelationItemViewModel
{
    public required string RelationId { get; init; }

    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    public required string TypeLabel { get; init; }

    public required string DisplayName { get; init; }

    public static ReleaseRelationItemViewModel FromRow(ReleaseRelationRow row) =>
        new()
        {
            RelationId = row.RelationId,
            TargetType = row.TargetType,
            TargetId = row.TargetId,
            TypeLabel = row.TargetType == Core.ReleaseRelationTarget.Feature ? "模块" : "任务",
            DisplayName = row.DisplayName,
        };
}

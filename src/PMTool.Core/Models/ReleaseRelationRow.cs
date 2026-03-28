namespace PMTool.Core.Models;

public sealed class ReleaseRelationRow
{
    public required string RelationId { get; init; }

    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    public required string DisplayName { get; init; }
}

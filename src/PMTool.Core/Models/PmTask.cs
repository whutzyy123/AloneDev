namespace PMTool.Core.Models;

public sealed class PmTask
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string FeatureId { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string TaskType { get; init; }
    public required string Status { get; init; }
    public string? Severity { get; init; }
    public double EstimatedHours { get; init; }
    public double ActualHours { get; init; }
    public string? CompletedAt { get; init; }
    public int SortValue { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
    public long RowVersion { get; init; }
}

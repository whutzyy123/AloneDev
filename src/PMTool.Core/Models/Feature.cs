using PMTool.Core;

namespace PMTool.Core.Models;

public sealed class Feature
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string Status { get; init; }
    public int Priority { get; init; } = FeaturePriorities.P2;
    public string AcceptanceCriteria { get; init; } = string.Empty;
    public string TechStack { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string? DueDate { get; init; }
    public string? AttachmentsPlaceholder { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
    public long RowVersion { get; init; }
}

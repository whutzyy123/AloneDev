namespace PMTool.Core.Models;

public sealed class Project
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string Status { get; init; }
    public string? Category { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
    public long RowVersion { get; init; }
}

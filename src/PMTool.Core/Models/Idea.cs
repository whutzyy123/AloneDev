namespace PMTool.Core.Models;

public sealed class Idea
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string Description { get; init; } = string.Empty;

    public string TechStack { get; init; } = string.Empty;

    public required string Status { get; init; }

    /// <summary>P0–P3，可为空。</summary>
    public string? Priority { get; init; }

    public string? LinkedProjectId { get; init; }

    public required string CreatedAt { get; init; }

    public required string UpdatedAt { get; init; }

    public bool IsDeleted { get; init; }

    public long RowVersion { get; init; }
}

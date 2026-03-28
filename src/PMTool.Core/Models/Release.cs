namespace PMTool.Core.Models;

public sealed class Release
{
    public required string Id { get; init; }

    public required string ProjectId { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    /// <summary>开始时间文本（与项目/特性日期策略一致）。</summary>
    public required string StartAt { get; init; }

    public required string EndAt { get; init; }

    public required string Status { get; init; }

    public required string CreatedAt { get; init; }

    public required string UpdatedAt { get; init; }

    public bool IsDeleted { get; init; }

    public long RowVersion { get; init; }
}

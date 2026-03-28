namespace PMTool.Core.Models;

public sealed class ReleaseListQuery
{
    public required string ProjectId { get; init; }

    public string? SearchText { get; init; }

    public string? StatusFilter { get; init; }

    public ReleaseSortField SortField { get; init; } = ReleaseSortField.UpdatedAt;

    public bool SortDescending { get; init; } = true;
}

public enum ReleaseSortField
{
    UpdatedAt,
    Name,
    StartAt,
}

namespace PMTool.Core.Models;

public sealed class FeatureListQuery
{
    public required string ProjectId { get; init; }
    public string? SearchText { get; init; }
    public string? StatusFilter { get; init; }
    public FeatureSortField SortField { get; init; } = FeatureSortField.UpdatedAt;
    public bool SortDescending { get; init; } = true;
}

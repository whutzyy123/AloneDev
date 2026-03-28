namespace PMTool.Core.Models;

public sealed class ProjectListItem
{
    public required Project Project { get; init; }
    public int FeatureCount { get; init; }
    public int TaskCount { get; init; }
    public int ReleaseCount { get; init; }
    public int DocumentCount { get; init; }
    public int LinkedIdeaCount { get; init; }
}

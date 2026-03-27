namespace PMTool.App.ViewModels;

public sealed partial class ProjectRowViewModel : ObservableObject
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Status { get; init; }

    public int FeatureCount { get; init; }

    public int TaskCount { get; init; }

    public int ReleaseCount { get; init; }

    public string Description { get; init; } = string.Empty;

    public string SummaryLine => $"{FeatureCount} 特性 · {TaskCount} 任务 · {ReleaseCount} 版本";
}

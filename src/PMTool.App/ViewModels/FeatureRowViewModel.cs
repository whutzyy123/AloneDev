using PMTool.Core;
using PMTool.Core.Models;

namespace PMTool.App.ViewModels;

public sealed partial class FeatureRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSearchHighlight;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Priority { get; init; }
    public string PriorityLabel { get; init; } = string.Empty;

    public bool IsPriorityP0 => Priority == FeaturePriorities.P0;

    public required string Status { get; init; }
    public required string UpdatedAt { get; init; }
    public string DescriptionPreview { get; init; } = string.Empty;

    public static FeatureRowViewModel FromFeature(Feature f) =>
        new()
        {
            Id = f.Id,
            Name = f.Name,
            Priority = f.Priority,
            PriorityLabel = FeaturePriorities.ToLabel(f.Priority),
            Status = f.Status,
            UpdatedAt = f.UpdatedAt,
            DescriptionPreview = Truncate(f.Description, 80),
        };

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
        {
            return s;
        }

        return s[..max] + "…";
    }
}

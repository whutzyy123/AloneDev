using Microsoft.UI.Xaml.Media;
using Windows.UI;
using PMTool.Core;
using PMTool.Core.Models;

namespace PMTool.App.ViewModels;

public sealed partial class IdeaRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSearchHighlight;

    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Status { get; init; }

    public string? PriorityLabel { get; init; }

    public string TechStack { get; init; } = string.Empty;

    public required string CreatedAt { get; init; }

    public required string UpdatedAt { get; init; }

    /// <summary>PRD 6.7.6：待评估蓝、已立项绿、已搁置灰。</summary>
    public SolidColorBrush StatusBrush { get; init; } = new(Color.FromArgb(255, 120, 120, 120));

    public static IdeaRowViewModel FromIdea(Idea idea)
    {
        var color = idea.Status switch
        {
            IdeaStatuses.Pending => Color.FromArgb(255, 0, 120, 212),
            IdeaStatuses.Approved => Color.FromArgb(255, 16, 124, 16),
            _ => Color.FromArgb(255, 120, 120, 120),
        };
        return new IdeaRowViewModel
        {
            Id = idea.Id,
            Title = idea.Title,
            Status = idea.Status,
            PriorityLabel = idea.Priority,
            TechStack = idea.TechStack,
            CreatedAt = idea.CreatedAt,
            UpdatedAt = idea.UpdatedAt,
            StatusBrush = new SolidColorBrush(color),
        };
    }
}

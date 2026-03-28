using PMTool.Core.Models;
using PMTool.Core.Ui;
using PMTool.Core.Validation;

namespace PMTool.App.ViewModels;

public sealed partial class ProjectRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSearchHighlight;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Status { get; init; }

    public int FeatureCount { get; init; }

    public int TaskCount { get; init; }

    public int ReleaseCount { get; init; }

    public int DocumentCount { get; init; }

    public int LinkedIdeaCount { get; init; }

    public string Description { get; init; } = string.Empty;

    public string TechStack { get; init; } = string.Empty;

    public IReadOnlyList<string> TechStackTags { get; init; } = Array.Empty<string>();

    public bool HasTechStackTags => TechStackTags.Count > 0;

    /// <summary>封面渐变调色板下标，与 <see cref="ProjectCoverPalette"/> 一致。</summary>
    public int CoverAccentIndex => ProjectCoverPalette.GetStableIndex(Id);

    public string SummaryLine =>
        $"{FeatureCount} 模块 · {TaskCount} 任务 · {ReleaseCount} 版本 · {DocumentCount} 文档 · {LinkedIdeaCount} 灵感";

    /// <summary>简易「生命体征」进度 0..1，供进度条展示信息密度。</summary>
    public double VitalityRatio
    {
        get
        {
            var n = FeatureCount + TaskCount + ReleaseCount + DocumentCount + LinkedIdeaCount;
            if (n <= 0)
            {
                return 0.05;
            }

            return Math.Clamp(n / 40.0, 0.08, 1);
        }
    }

    public static ProjectRowViewModel FromItem(ProjectListItem item) => new()
    {
        Id = item.Project.Id,
        Name = item.Project.Name,
        Status = item.Project.Status,
        FeatureCount = item.FeatureCount,
        TaskCount = item.TaskCount,
        ReleaseCount = item.ReleaseCount,
        DocumentCount = item.DocumentCount,
        LinkedIdeaCount = item.LinkedIdeaCount,
        Description = item.Project.Description,
        TechStack = item.Project.TechStack ?? string.Empty,
        TechStackTags = ProjectFieldValidator.ParseTechStackTags(item.Project.TechStack),
    };
}

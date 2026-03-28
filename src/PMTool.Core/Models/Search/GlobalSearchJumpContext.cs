namespace PMTool.Core.Models.Search;

/// <summary>跨模块跳转时恢复下拉与选中项所需上下文。</summary>
public sealed record GlobalSearchJumpContext
{
    public string? ProjectId { get; init; }

    public string? FeatureId { get; init; }
}

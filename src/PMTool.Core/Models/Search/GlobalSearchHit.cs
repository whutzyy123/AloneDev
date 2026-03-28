namespace PMTool.Core.Models.Search;

public sealed class GlobalSearchHit
{
    public required GlobalSearchModule Module { get; init; }

    public required string EntityId { get; init; }

    public required string Title { get; init; }

    /// <summary>摘要纯文本，供 UI 分段高亮。</summary>
    public required string Snippet { get; init; }

    public int MatchScore { get; init; }

    public required string UpdatedAt { get; init; }

    public GlobalSearchJumpContext Jump { get; init; } = new();
}

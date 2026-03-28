namespace PMTool.Core.Models.Search;

public sealed record GlobalSearchResponse(
    IReadOnlyList<GlobalSearchHit> Hits,
    int ElapsedMilliseconds,
    bool ShowPerformanceHint);

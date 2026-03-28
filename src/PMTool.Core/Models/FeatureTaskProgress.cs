namespace PMTool.Core.Models;

/// <summary>PRD 6.4.2：完成数 / 分母排除「已取消」。</summary>
public readonly record struct FeatureTaskProgress(int CompletedCount, int TotalExcludingCancelled)
{
    public double Percent =>
        TotalExcludingCancelled <= 0 ? 0 : Math.Round(CompletedCount * 100.0 / TotalExcludingCancelled, 1, MidpointRounding.AwayFromZero);
}

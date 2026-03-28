namespace PMTool.Core.Models;

/// <summary>PRD 6.5.2：F/f 为关联特性总数与「已完成|已上线」数；T/t 为关联任务总数与已完成数。</summary>
public readonly record struct ReleaseProgressStats(
    int TotalFeatures,
    int CompletedFeatures,
    int TotalTasks,
    int CompletedTasks,
    double Percent)
{
    /// <summary>PRD：F+T==0 则 0%；否则 (Rf+Rt)/2，Rf/Rt 在分母为 0 时为 0。</summary>
    public static double ComputePercent(int F, int f, int T, int t)
    {
        if (F + T == 0)
        {
            return 0;
        }

        var rf = F == 0 ? 0.0 : 100.0 * f / F;
        var rt = T == 0 ? 0.0 : 100.0 * t / T;
        return (rf + rt) / 2.0;
    }
}

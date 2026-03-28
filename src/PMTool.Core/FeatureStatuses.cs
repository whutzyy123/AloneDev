namespace PMTool.Core;

public static class FeatureStatuses
{
    public const string ToPlan = "待规划";
    public const string InProgress = "进行中";
    public const string Done = "已完成";
    public const string Released = "已上线";

    public static IReadOnlyList<string> All { get; } = [ToPlan, InProgress, Done, Released];
}

namespace PMTool.Core;

public static class ReleaseStatuses
{
    public const string NotStarted = "未开始";

    public const string InProgress = "进行中";

    public const string Ended = "已结束";

    public const string Cancelled = "已取消";

    public static IReadOnlyList<string> All { get; } = [NotStarted, InProgress, Ended, Cancelled];
}

namespace PMTool.Core;

public static class TaskStatuses
{
    public const string NotStarted = "未开始";
    public const string InProgress = "进行中";
    public const string Done = "已完成";
    public const string Cancelled = "已取消";

    public static IReadOnlyList<string> All { get; } = [NotStarted, InProgress, Done, Cancelled];
}

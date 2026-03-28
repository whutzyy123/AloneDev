namespace PMTool.Core;

public static class IdeaStatuses
{
    public const string Pending = "待评估";

    public const string Approved = "已立项";

    public const string Shelved = "已搁置";

    public static IReadOnlyList<string> All { get; } = [Pending, Approved, Shelved];

    public static bool IsKnown(string? status) =>
        status is Pending or Approved or Shelved;
}

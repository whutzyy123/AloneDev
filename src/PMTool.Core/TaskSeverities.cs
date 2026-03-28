namespace PMTool.Core;

public static class TaskSeverities
{
    public const string Blocker = "Blocker";
    public const string Major = "Major";
    public const string Minor = "Minor";

    public static IReadOnlyList<string> All { get; } = [Blocker, Major, Minor];
}

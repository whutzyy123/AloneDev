namespace PMTool.Core;

public static class TaskTypes
{
    public const string Feature = "Feature";
    public const string Bug = "Bug";
    public const string Refactor = "Refactor";
    public const string Research = "Research";

    public static IReadOnlyList<string> All { get; } = [Feature, Bug, Refactor, Research];
}

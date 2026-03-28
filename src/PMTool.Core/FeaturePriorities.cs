namespace PMTool.Core;

public static class FeaturePriorities
{
    public const int P0 = 0;
    public const int P1 = 1;
    public const int P2 = 2;
    public const int P3 = 3;

    public static int Normalize(int priority) => priority switch
    {
        < P0 => P0,
        > P3 => P3,
        _ => priority,
    };

    public static string ToLabel(int priority) => $"P{Normalize(priority)}";
}

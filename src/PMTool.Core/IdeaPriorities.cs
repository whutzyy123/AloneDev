namespace PMTool.Core;

public static class IdeaPriorities
{
    public const string P0 = "P0";

    public const string P1 = "P1";

    public const string P2 = "P2";

    public const string P3 = "P3";

    public static bool IsKnown(string? p) =>
        p is P0 or P1 or P2 or P3 or null or "";
}

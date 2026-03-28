namespace PMTool.Core.Validation;

internal static class SharedEntityNameRules
{
    private static readonly char[] IllegalNameChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    internal static string Validate(string name, string entityLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException($"{entityLabel}名称不可为空。", nameof(name));
        }

        if (trimmed.Length > 100)
        {
            throw new ArgumentException($"{entityLabel}名称不可超过 100 个字符。", nameof(name));
        }

        if (trimmed.IndexOfAny(IllegalNameChars) >= 0)
        {
            throw new ArgumentException($"{entityLabel}名称不能包含以下字符：\\ / : * ? \" < > |", nameof(name));
        }

        return trimmed;
    }
}

namespace PMTool.Core.Validation;

public static class FeatureFieldValidator
{
    private const int LongTextMax = 2000;

    public static string ValidateName(string name) => SharedEntityNameRules.Validate(name, "模块");

    public static string ValidateDescription(string? description)
    {
        var s = description ?? string.Empty;
        if (s.Length > LongTextMax)
        {
            throw new ArgumentException($"模块描述不可超过 {LongTextMax} 个字符。", nameof(description));
        }

        return s;
    }

    public static string ValidateLongText(string? text, string fieldDisplayName)
    {
        var s = text ?? string.Empty;
        if (s.Length > LongTextMax)
        {
            throw new ArgumentException($"{fieldDisplayName}不可超过 {LongTextMax} 个字符。", nameof(text));
        }

        return s;
    }
}

namespace PMTool.Core.Validation;

public static class ReleaseFieldValidator
{
    private const int DescriptionMax = 1000;

    public static string ValidateName(string name) => SharedEntityNameRules.Validate(name, "版本");

    public static string ValidateDescription(string? description)
    {
        var s = description ?? string.Empty;
        if (s.Length > DescriptionMax)
        {
            throw new ArgumentException($"版本描述不可超过 {DescriptionMax} 个字符。", nameof(description));
        }

        return s;
    }

    /// <summary>开始/结束时间：非空文本；PRD 校验「开始不晚于结束」在调用方比较解析后的顺序。</summary>
    public static string ValidateRequiredDateText(string? text, string fieldDisplayName)
    {
        var s = (text ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            throw new ArgumentException($"{fieldDisplayName}不可为空。", nameof(text));
        }

        return s;
    }
}

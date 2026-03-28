namespace PMTool.Core.Validation;

public static class ProjectFieldValidator
{
    public static string ValidateName(string name) => SharedEntityNameRules.Validate(name, "项目");

    public static string ValidateDescription(string? description)
    {
        var s = description ?? string.Empty;
        if (s.Length > 500)
        {
            throw new ArgumentException("项目描述不可超过 500 个字符。", nameof(description));
        }

        return s;
    }
}

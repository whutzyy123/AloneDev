namespace PMTool.Core.Validation;

public static class AccountNameValidator
{
    /// <summary>校验并返回规范化名称（Trim）。非法则抛 <see cref="ArgumentException"/>。</summary>
    public static string NormalizeAndValidate(string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        var name = accountName.Trim();
        if (name.Length == 0)
        {
            throw new ArgumentException("工作空间名称不能为空。", nameof(accountName));
        }

        if (name is "." or "..")
        {
            throw new ArgumentException("工作空间名称不能使用 “.” 或 “..”。", nameof(accountName));
        }

        if (name.Contains('\\', StringComparison.Ordinal) || name.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("工作空间名称不能包含路径分隔符。", nameof(accountName));
        }

        var invalid = Path.GetInvalidFileNameChars();
        if (name.IndexOfAny(invalid) >= 0)
        {
            throw new ArgumentException("工作空间名称包含非法文件名字符。", nameof(accountName));
        }

        return name;
    }
}

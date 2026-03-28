using System.Text.RegularExpressions;

namespace PMTool.Core.Validation;

/// <summary>供 PRAGMA / DDL 拼接前的 SQLite 标识符校验（仅允许未被引号的普通表名形态）。</summary>
public static partial class SqliteIdentifierValidator
{
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeTableNameRegex();

    public static void ThrowIfInvalidTableName(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        if (!SafeTableNameRegex().IsMatch(tableName))
        {
            throw new ArgumentException(
                "表名包含非法字符，仅允许字母、数字与下划线且不得以数字开头。",
                nameof(tableName));
        }
    }
}

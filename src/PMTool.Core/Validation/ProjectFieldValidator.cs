using System.IO;
using System.Text;

namespace PMTool.Core.Validation;

public static class ProjectFieldValidator
{
    private const int TechStackMaxTotalLength = 512;
    private const int TechStackMaxTagLength = 32;
    private const int TechStackMaxTagCount = 24;
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

    /// <summary>空或空白表示不关联；否则须为存在且含 .git 的目录。</summary>
    public static string? ValidateOptionalLocalGitRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var t = path.Trim();
        if (!Directory.Exists(t))
        {
            throw new ArgumentException("本地 Git 路径不是有效文件夹。", nameof(path));
        }

        var gitFile = Path.Combine(t, ".git");
        if (!File.Exists(gitFile) && !Directory.Exists(gitFile))
        {
            throw new ArgumentException("所选文件夹不是 Git 仓库根目录（未找到 .git）。", nameof(path));
        }

        return t;
    }

    /// <summary>解析已存储的技术栈字符串为标签列表（用于 UI）。不修改数据库内容。</summary>
    public static IReadOnlyList<string> ParseTechStackTags(string? stored)
    {
        var normalized = NormalizeToTagsList(stored ?? string.Empty);
        return normalized.Count == 0 ? Array.Empty<string>() : normalized.ToArray();
    }

    /// <summary>校验并规范化技术栈；空视为空串。</summary>
    public static string ValidateTechStack(string? techStack)
    {
        var raw = techStack ?? string.Empty;
        if (raw.Length > TechStackMaxTotalLength)
        {
            throw new ArgumentException($"技术栈原文不可超过 {TechStackMaxTotalLength} 个字符。", nameof(techStack));
        }

        var tags = NormalizeToTagsList(raw);
        if (tags.Count > TechStackMaxTagCount)
        {
            throw new ArgumentException($"技术栈标签最多 {TechStackMaxTagCount} 个。", nameof(techStack));
        }

        foreach (var tag in tags)
        {
            if (tag.Length > TechStackMaxTagLength)
            {
                throw new ArgumentException($"单个标签「{tag}」不可超过 {TechStackMaxTagLength} 个字符。", nameof(techStack));
            }

            foreach (var c in tag)
            {
                if (!IsAllowedTechStackChar(c))
                {
                    throw new ArgumentException($"标签「{tag}」包含不允许的字符，仅支持字母、数字、中文、空格与 # . + - 。", nameof(techStack));
                }
            }
        }

        return tags.Count == 0 ? string.Empty : string.Join(", ", tags);
    }

    private static bool IsAllowedTechStackChar(char c) =>
        char.IsLetter(c) || char.IsDigit(c) || c is ' ' or '#' or '.' or '+' or '-' or '_';

    private static List<string> NormalizeToTagsList(string raw)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(8);
        var cur = new StringBuilder();
        foreach (var ch in raw)
        {
            if (ch is ',' or ';' or '，')
            {
                FlushTag(cur, seen, result);
                continue;
            }

            _ = cur.Append(ch);
        }

        FlushTag(cur, seen, result);
        return result;
    }

    private static void FlushTag(StringBuilder cur, HashSet<string> seen, List<string> result)
    {
        var s = cur.ToString().Trim();
        cur.Clear();
        if (s.Length == 0 || !seen.Add(s))
        {
            return;
        }

        result.Add(s);
    }
}

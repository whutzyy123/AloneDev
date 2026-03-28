using System.Text;

namespace PMTool.Core;

/// <summary>PRD 6.10.7：过滤路径非法字符；返回实际参与 LIKE 的关键词。</summary>
public static class GlobalSearchKeywordNormalizer
{
    private static readonly char[] FilteredChars = ['\\', '/', ':', '*', '?'];

    public static (string? Needle, bool HadFilteredChars) Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, false);
        }

        var hadFilter = false;
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw.AsSpan().Trim())
        {
            if (Array.IndexOf(FilteredChars, c) >= 0)
            {
                hadFilter = true;
                continue;
            }

            _ = sb.Append(c);
        }

        var s = sb.ToString().Trim();
        if (s.Length == 0)
        {
            return (null, hadFilter);
        }

        return (s, hadFilter);
    }
}

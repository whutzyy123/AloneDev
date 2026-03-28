namespace PMTool.Core.Validation;

/// <summary>账户内备份子目录相对路径（相对于各账号根目录），禁止 <c>.</c> / <c>..</c> 段与绝对路径形态。</summary>
public static class BackupDirectoryRelativeValidator
{
    /// <summary>校验相对路径；非法则抛 <see cref="ArgumentException"/>。</summary>
    public static string NormalizeAndValidate(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("备份相对目录不能为空。", nameof(relativePath));
        }

        foreach (var ch in Path.GetInvalidPathChars())
        {
            if (relativePath.Contains(ch, StringComparison.Ordinal))
            {
                throw new ArgumentException("备份相对目录包含非法路径字符。", nameof(relativePath));
            }
        }

        var t = relativePath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (t.Length == 0)
        {
            throw new ArgumentException("备份相对目录不能为空。", nameof(relativePath));
        }

        if (Path.IsPathRooted(t))
        {
            throw new ArgumentException("备份相对目录不能为绝对路径。", nameof(relativePath));
        }

        var segments = t.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            if (seg is "." or "..")
            {
                throw new ArgumentException("备份相对目录不能包含 “.” 或 “..” 段。", nameof(relativePath));
            }

            if (seg.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("备份相对目录包含非法文件夹名字符。", nameof(relativePath));
            }
        }

        return t;
    }
}

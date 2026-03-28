namespace PMTool.Core.IO;

/// <summary>基于 <see cref="Path.GetRelativePath"/> 的路径包含判断（避免仅用 <c>StartsWith</c> 的前缀绕过）。</summary>
public static class PathSecurity
{
    /// <summary>
    /// 判断 <paramref name="candidatePath"/> 规范化后是否落在 <paramref name="parentDirectory"/> 之内或与其为同一路径（均为规范绝对路径语义）。
    /// </summary>
    public static bool IsPathWithinDirectory(string parentDirectory, string candidatePath)
    {
        var parent = Path.GetFullPath(NormalizeDirForFullPath(parentDirectory));
        var candidate = Path.GetFullPath(candidatePath);
        var relative = Path.GetRelativePath(parent, candidate);
        if (Path.IsPathFullyQualified(relative))
        {
            return false;
        }

        return !relative.StartsWith("..", StringComparison.Ordinal);
    }

    private static string NormalizeDirForFullPath(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return directory;
        }

        var t = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return t.Length == 0 ? directory : t;
    }
}

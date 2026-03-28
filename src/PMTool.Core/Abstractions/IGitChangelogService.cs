namespace PMTool.Core.Abstractions;

/// <summary>只读本地 Git 提交历史，生成版本变更说明（Markdown）；不访问网络。</summary>
public interface IGitChangelogService
{
    /// <param name="repositoryRoot">含 .git 的仓库根目录。</param>
    /// <param name="releaseStartAt">版本开始时间文本（yyyy-MM-dd HH:mm）。</param>
    /// <param name="releaseEndAt">版本结束时间文本。</param>
    Task<string> BuildMarkdownChangelogAsync(
        string repositoryRoot,
        string releaseStartAt,
        string releaseEndAt,
        CancellationToken cancellationToken = default);
}

using System.Globalization;
using System.Text;
using LibGit2Sharp;
using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Git;

public sealed class GitChangelogService : IGitChangelogService
{
    public Task<string> BuildMarkdownChangelogAsync(
        string repositoryRoot,
        string releaseStartAt,
        string releaseEndAt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ArgumentException("未配置本地 Git 仓库路径。", nameof(repositoryRoot));
        }

        var root = repositoryRoot.Trim();
        var start = ParseReleaseInstant(releaseStartAt);
        var end = ParseReleaseInstant(releaseEndAt);
        if (end < start)
        {
            throw new InvalidOperationException("版本结束时间早于开始时间，无法按区间筛选提交。");
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var repo = new Repository(root);
                    var sb = new StringBuilder();
                    _ = sb.AppendLine("### Git 提交");
                    var count = 0;
                    var filter = new CommitFilter { SortBy = CommitSortStrategies.Time };
                    foreach (var c in repo.Commits.QueryBy(filter))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (c.Parents.Count() > 1)
                        {
                            continue;
                        }

                        var when = c.Committer.When;
                        if (when < start || when > end)
                        {
                            continue;
                        }

                        var msg = c.Message.Split('\n')[0].Trim();
                        if (string.IsNullOrEmpty(msg))
                        {
                            msg = "（无说明）";
                        }

                        var shortSha = c.Sha.Length >= 7 ? c.Sha[..7] : c.Sha;
                        _ = sb.AppendLine($"- {msg} ({shortSha})");
                        count++;
                    }

                    if (count == 0)
                    {
                        _ = sb.AppendLine("- （该时间范围内无非合并提交）");
                    }

                    return sb.ToString().TrimEnd();
                }
                catch (RepositoryNotFoundException ex)
                {
                    throw new InvalidOperationException("无法打开该路径下的 Git 仓库。", ex);
                }
                catch (LibGit2SharpException ex)
                {
                    throw new InvalidOperationException($"读取 Git 历史失败：{ex.Message}", ex);
                }
            },
            cancellationToken);
    }

    private static DateTimeOffset ParseReleaseInstant(string text)
    {
        var s = (text ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            throw new ArgumentException("版本时间不可为空。", nameof(text));
        }

        if (!DateTime.TryParseExact(
                s,
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var dt))
        {
            throw new ArgumentException("版本时间格式无效，请使用「yyyy-MM-dd HH:mm」。", nameof(text));
        }

        return new DateTimeOffset(dt);
    }
}

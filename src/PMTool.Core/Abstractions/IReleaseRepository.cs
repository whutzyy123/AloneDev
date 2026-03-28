using PMTool.Core.Models;

namespace PMTool.Core.Abstractions;

public interface IReleaseRepository
{
    Task<IReadOnlyList<Release>> ListAsync(ReleaseListQuery query, CancellationToken cancellationToken = default);

    /// <summary>全部未删除版本（导出等）。</summary>
    Task<IReadOnlyList<Release>> ListAllActiveAsync(CancellationToken cancellationToken = default);

    Task<Release?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task InsertAsync(Release release, CancellationToken cancellationToken = default);

    Task UpdateAsync(Release release, CancellationToken cancellationToken = default);

    /// <summary>软删除版本并删除其关联行。</summary>
    Task SoftDeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReleaseRelationRow>> ListRelationsAsync(string releaseId, CancellationToken cancellationToken = default);

    Task AddRelationAsync(
        string releaseId,
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default);

    Task RemoveRelationAsync(
        string releaseId,
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default);

    Task<ReleaseProgressStats> GetProgressAsync(string releaseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, ReleaseProgressStats>> GetProgressBatchAsync(
        IReadOnlyList<string> releaseIds,
        CancellationToken cancellationToken = default);
}

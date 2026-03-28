using PMTool.Core.Models;

namespace PMTool.Core.Abstractions;

public interface IFeatureRepository
{
    Task<IReadOnlyList<Feature>> ListAsync(FeatureListQuery query, CancellationToken cancellationToken = default);

    /// <summary>全部未删除特性（导出等）。</summary>
    Task<IReadOnlyList<Feature>> ListAllActiveAsync(CancellationToken cancellationToken = default);

    Task<Feature?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task InsertAsync(Feature feature, CancellationToken cancellationToken = default);

    Task UpdateAsync(Feature feature, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(string id, CancellationToken cancellationToken = default);
}

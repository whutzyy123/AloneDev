using PMTool.Core.Models;

namespace PMTool.Core.Abstractions;

public interface IDocumentRepository
{
    Task<IReadOnlyList<PmDocument>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<PmDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task InsertAsync(PmDocument document, CancellationToken cancellationToken = default);

    Task UpdateContentAsync(
        string id,
        string content,
        string contentFormat,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);

    Task UpdateMetadataAsync(
        string id,
        string name,
        bool isCodeSnippet,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);

    /// <summary>一次更新名称、代码片段标记与正文，仅递增一次 <c>row_version</c>。</summary>
    Task UpdateFullAsync(
        string id,
        string name,
        bool isCodeSnippet,
        string content,
        string contentFormat,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(string id, long expectedRowVersion, CancellationToken cancellationToken = default);
}

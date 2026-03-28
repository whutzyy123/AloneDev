using PMTool.Core.Models;

namespace PMTool.Core.Abstractions;

public interface IIdeaRepository
{
    Task<IReadOnlyList<Idea>> ListAsync(IdeaListQuery query, CancellationToken cancellationToken = default);

    Task<Idea?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task InsertAsync(Idea idea, CancellationToken cancellationToken = default);

    Task UpdateAsync(Idea idea, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(string id, long expectedRowVersion, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IdeaDocumentLink>> ListDocumentLinksAsync(string ideaId, CancellationToken cancellationToken = default);

    Task AddDocumentLinkAsync(string ideaId, string documentId, CancellationToken cancellationToken = default);

    Task RemoveDocumentLinkAsync(string linkId, long expectedRowVersion, CancellationToken cancellationToken = default);
}

namespace PMTool.Core.Models;

public sealed class IdeaDocumentLink
{
    public required string Id { get; init; }

    public required string IdeaId { get; init; }

    public required string DocumentId { get; init; }

    /// <summary>展示用，来自 documents.name。</summary>
    public string DocumentName { get; init; } = string.Empty;

    public required string CreatedAt { get; init; }

    public bool IsDeleted { get; init; }

    public long RowVersion { get; init; }
}

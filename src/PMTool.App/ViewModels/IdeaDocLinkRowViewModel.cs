namespace PMTool.App.ViewModels;

public sealed class IdeaDocLinkRowViewModel
{
    public required string LinkId { get; init; }

    public required string DocumentId { get; init; }

    public required string DocumentName { get; init; }

    public long RowVersion { get; init; }
}

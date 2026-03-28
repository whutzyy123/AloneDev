namespace PMTool.Core.Models;

public enum IdeaSortField
{
    UpdatedAt,
    Title,
    Status,
}

public sealed record IdeaListQuery(
    string? SearchText,
    string? StatusFilter,
    string? PriorityFilter,
    IdeaSortField SortField,
    bool SortDescending);

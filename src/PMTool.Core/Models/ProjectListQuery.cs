namespace PMTool.Core.Models;

public enum ProjectSortField
{
    Name,
    CreatedAt,
    UpdatedAt,
}

/// <param name="StatusFilter">null = 全部；否则 <see cref="ProjectStatuses"/> 值。</param>
public sealed record ProjectListQuery(
    string? StatusFilter,
    string? SearchText,
    ProjectSortField SortField,
    bool SortDescending);

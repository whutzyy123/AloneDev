namespace PMTool.Core.Models;

public enum CodeSnippetListScope
{
    /// <summary>全部片段（含全局与各项目）。</summary>
    All,

    /// <summary>仅 <see cref="DocumentRelateTypes.Global"/> 且无 project_id。</summary>
    GlobalOnly,

    /// <summary>指定项目下的片段，并始终包含全局片段。</summary>
    ByProject,
}

public enum CodeSnippetSortField
{
    UpdatedAt,
    Name,
}

/// <summary>代码片段列表筛选与排序（仓储查询）。</summary>
public sealed class CodeSnippetListQuery
{
    public CodeSnippetListScope Scope { get; init; } = CodeSnippetListScope.All;

    /// <summary>
    /// 当 <see cref="Scope"/> 为 <see cref="CodeSnippetListScope.ByProject"/> 时使用；否则应为 <c>null</c>。
    /// </summary>
    public string? ProjectFilterId { get; init; }

    public string? SearchText { get; init; }

    public CodeSnippetSortField SortField { get; init; } = CodeSnippetSortField.UpdatedAt;

    public bool SortDescending { get; init; } = true;
}

namespace PMTool.Core.Models;

public sealed class PmDocument
{
    public required string Id { get; init; }

    /// <summary>可空：全局文档无项目。</summary>
    public string? ProjectId { get; init; }

    public string? FeatureId { get; init; }

    public required string Name { get; init; }

    public required string RelateType { get; init; }

    public string Content { get; init; } = string.Empty;

    public required string ContentFormat { get; init; }

    public bool IsCodeSnippet { get; init; }

    /// <summary>高亮语言标识（与 highlight.js 一致，如 <c>csharp</c>）；非代码片段或未设置时为 <c>null</c>。</summary>
    public string? SnippetLanguage { get; init; }

    public required string CreatedAt { get; init; }

    public required string UpdatedAt { get; init; }

    public bool IsDeleted { get; init; }

    public long RowVersion { get; init; }
}

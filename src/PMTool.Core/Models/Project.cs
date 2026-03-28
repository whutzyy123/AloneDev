namespace PMTool.Core.Models;

public sealed class Project
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string Status { get; init; }
    public string? Category { get; init; }

    /// <summary>技术栈标签，规范化后以逗号+空格连接（如 Vue, Rust）。</summary>
    public string TechStack { get; init; } = string.Empty;

    /// <summary>可选：本地 Git 仓库根目录（含 .git），用于版本页生成变更说明。仅本机路径。</summary>
    public string? LocalGitRoot { get; init; }

    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
    public long RowVersion { get; init; }
}

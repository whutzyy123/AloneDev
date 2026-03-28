namespace PMTool.Core.Abstractions;

/// <summary>
/// Resolves the root folder for per-account data directories (<c>.../PMProjectTool/Data</c> per PRD).
/// </summary>
public interface IDataRootProvider
{
    string GetDataRootPath();

    /// <summary>迁移成功或启动解析锚点后更新；须为绝对路径。</summary>
    void SetDataRootPath(string absolutePath);
}

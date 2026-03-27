namespace PMTool.Core.Abstractions;

/// <summary>
/// Resolves the root folder for per-account data directories (<c>.../PMProjectTool/Data</c> per PRD).
/// </summary>
public interface IDataRootProvider
{
    string GetDataRootPath();
}

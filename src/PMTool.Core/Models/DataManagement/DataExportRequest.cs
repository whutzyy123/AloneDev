namespace PMTool.Core.Models.DataManagement;

public sealed class DataExportRequest
{
    public DataExportModule Modules { get; init; }

    public DataExportFormat Format { get; init; }

    /// <summary>导出目录（绝对路径）。</summary>
    public required string OutputDirectory { get; init; }
}

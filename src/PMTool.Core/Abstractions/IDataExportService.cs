using PMTool.Core.Models.DataManagement;

namespace PMTool.Core.Abstractions;

public interface IDataExportService
{
    Task ExportAsync(
        DataExportRequest request,
        IProgress<(string message, int percent)>? progress,
        CancellationToken cancellationToken = default);
}

using PMTool.Application.Abstractions;
using PMTool.Core.Abstractions;

namespace PMTool.Application.Services;

public sealed class AppInitializationService(
    IDataRootProvider dataRootProvider,
    ICurrentAccountContext accountContext) : IAppInitializationService
{
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Directory.CreateDirectory(dataRootProvider.GetDataRootPath());
            _ = Directory.CreateDirectory(accountContext.GetAccountDirectoryPath());
            var logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PMProjectTool", "Logs");
            _ = Directory.CreateDirectory(logsDir);
        }, cancellationToken);
    }
}

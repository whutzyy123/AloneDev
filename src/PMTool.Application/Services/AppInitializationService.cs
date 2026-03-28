using PMTool.Application.Abstractions;
using PMTool.Core.Abstractions;

namespace PMTool.Application.Services;

public sealed class AppInitializationService(
    IAccountManagementService accountManagement,
    IDataRootProvider dataRootProvider,
    ICurrentAccountContext accountContext,
    IAppConfigStore appConfigStore) : IAppInitializationService
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _ = await appConfigStore.TryRepairOnCorruptAsync(cancellationToken).ConfigureAwait(false);
        await accountManagement.LoadCatalogAndApplyLastAccountAsync(cancellationToken).ConfigureAwait(false);
        _ = Directory.CreateDirectory(dataRootProvider.GetDataRootPath());
        _ = Directory.CreateDirectory(accountContext.GetAccountDirectoryPath());
        _ = await appConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PMProjectTool", "Logs");
        _ = Directory.CreateDirectory(logsDir);
    }
}

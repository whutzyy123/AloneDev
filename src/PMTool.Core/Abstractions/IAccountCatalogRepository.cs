using PMTool.Core.Models;

namespace PMTool.Core.Abstractions;

public interface IAccountCatalogRepository
{
    Task<AccountCatalog> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AccountCatalog catalog, CancellationToken cancellationToken = default);
}

using PMTool.Core.Models.Search;

namespace PMTool.App.Services;

public interface IGlobalSearchNavigationService
{
    Task NavigateToHitAsync(GlobalSearchHit hit, CancellationToken cancellationToken = default);
}

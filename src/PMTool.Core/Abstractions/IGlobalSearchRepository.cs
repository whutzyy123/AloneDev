using PMTool.Core.Models.Search;

namespace PMTool.Core.Abstractions;

public interface IGlobalSearchRepository
{
    Task<GlobalSearchResponse> SearchAsync(GlobalSearchRequest request, CancellationToken cancellationToken = default);
}

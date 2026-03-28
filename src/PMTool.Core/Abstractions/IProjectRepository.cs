using PMTool.Core.Models;

namespace PMTool.Core.Abstractions;

public interface IProjectRepository
{
    Task<IReadOnlyList<ProjectListItem>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken = default);
    Task<Project?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task InsertAsync(Project project, CancellationToken cancellationToken = default);
    Task UpdateCoreAsync(Project project, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> ExistsNameInStatusAsync(string name, string status, string? excludeProjectId, CancellationToken cancellationToken = default);
}

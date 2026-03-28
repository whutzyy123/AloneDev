namespace PMTool.Core.Abstractions;

public interface IProjectDeletionGuard
{
    Task<bool> HasBlockingAssociationsAsync(string projectId, CancellationToken cancellationToken = default);
}

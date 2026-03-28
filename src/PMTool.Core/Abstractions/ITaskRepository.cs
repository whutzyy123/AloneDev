using PMTool.Core.Models;

namespace PMTool.Core.Abstractions;

public interface ITaskRepository
{
    /// <summary>项目下全部未删除任务（用于版本关联选择器等）。</summary>
    Task<IReadOnlyList<PmTask>> ListByProjectAsync(string projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PmTask>> ListAsync(TaskListQuery query, CancellationToken cancellationToken = default);

    /// <summary>全部未删除任务（导出等）。</summary>
    Task<IReadOnlyList<PmTask>> ListAllActiveAsync(CancellationToken cancellationToken = default);

    Task<PmTask?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task InsertAsync(PmTask task, CancellationToken cancellationToken = default);

    Task UpdateAsync(PmTask task, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(string id, CancellationToken cancellationToken = default);

    Task MoveWithinFeatureAsync(string taskId, int direction, CancellationToken cancellationToken = default);

    Task<FeatureTaskProgress> GetFeatureProgressAsync(string featureId, CancellationToken cancellationToken = default);
}

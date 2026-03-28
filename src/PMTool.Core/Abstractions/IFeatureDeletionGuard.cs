namespace PMTool.Core.Abstractions;

public interface IFeatureDeletionGuard
{
    /// <summary>存在未删除且关联本特性的任务时不可删除特性。</summary>
    Task<bool> HasBlockingTasksAsync(string featureId, CancellationToken cancellationToken = default);
}

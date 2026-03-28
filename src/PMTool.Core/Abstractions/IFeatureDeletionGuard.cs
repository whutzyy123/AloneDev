namespace PMTool.Core.Abstractions;

public interface IFeatureDeletionGuard
{
    /// <summary>存在未删除且关联本模块的任务时不可删除模块。</summary>
    Task<bool> HasBlockingTasksAsync(string featureId, CancellationToken cancellationToken = default);
}

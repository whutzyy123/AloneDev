using PMTool.Core.Models.Settings;

namespace PMTool.Core.Abstractions;

public interface IDataRootMigrationService
{
    /// <summary>目标须为空目录且可写。</summary>
    Task ValidateTargetPathAsync(string absolutePath, CancellationToken cancellationToken = default);

    /// <summary>是否存在未完成迁移状态。</summary>
    Task<DataRootMigrationState?> GetPendingStateAsync(CancellationToken cancellationToken = default);

    Task ClearPendingStateAsync(CancellationToken cancellationToken = default);

    /// <summary>从当前生效根复制到新根，关闭 DB；成功则更新锚点与 config.DataPath。<paramref name="targetRootPath"/> 为 null 时表示继续未完成的迁移。</summary>
    Task RunAsync(
        string? targetRootPath,
        IProgress<(string message, int percent)>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>放弃迁移：删除状态文件，不改动锚点。</summary>
    Task RollbackPendingOnlyAsync(CancellationToken cancellationToken = default);
}

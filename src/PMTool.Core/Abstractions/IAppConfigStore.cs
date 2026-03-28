using PMTool.Core.Models.Settings;

namespace PMTool.Core.Abstractions;

public interface IAppConfigStore
{
    /// <summary>载入或创建默认；可能将损坏文件替换为默认。</summary>
    Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>损坏时写回默认配置，返回是否曾损坏。</summary>
    Task<bool> TryRepairOnCorruptAsync(CancellationToken cancellationToken = default);
}

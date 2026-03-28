namespace PMTool.Core.Abstractions;

/// <summary>
/// 稳定锚点：%LocalAppData%\AloneDev\config.anchor.json，仅存当前生效数据根绝对路径，避免迁盘后记不得新根。
/// </summary>
public interface IConfigAnchorStore
{
    Task<string?> GetEffectiveDataRootAsync(CancellationToken cancellationToken = default);

    Task SetEffectiveDataRootAsync(string absolutePath, CancellationToken cancellationToken = default);
}

using PMTool.Core.Models.DataManagement;

namespace PMTool.Core.Abstractions;

public interface IAccountBackupService
{
    /// <param name="targetDirectory">为 null 时使用账户目录下配置的备份子目录。</param>
    /// <param name="retentionCount">自动删除更早的「备份_*.db」；≤0 表示不修剪。</param>
    Task<BackupFileInfo> CreateBackupAsync(
        string? targetDirectory,
        int retentionCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken cancellationToken = default);

    Task DeleteBackupAsync(string absolutePath, CancellationToken cancellationToken = default);

    Task<BackupVerificationResult> VerifyDatabaseFileAsync(string absolutePath, CancellationToken cancellationToken = default);

    /// <summary>恢复成功后需提示用户重启应用。失败时已尽力回滚当前库文件。</summary>
    Task<RestoreResult> RestoreFromBackupAsync(string backupFilePath, CancellationToken cancellationToken = default);
}

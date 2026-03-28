namespace PMTool.Core.Models.DataManagement;

/// <summary>按账户持久化（JSON）。</summary>
public sealed class DataManagementSettings
{
    public bool AutoBackupEnabled { get; set; }

    public int RetentionCount { get; set; } = 7;

    /// <summary>启动补备份：距上次成功备份超过此时长（小时）则补备。</summary>
    public int MaxBackupIntervalHours { get; set; } = 24;

    /// <summary>相对账户根目录，默认 Backup。</summary>
    public string BackupDirectoryRelative { get; set; } = "Backup";

    public DateTime? LastSuccessfulBackupUtc { get; set; }
}

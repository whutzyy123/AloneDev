using System.Text.Json.Serialization;

namespace PMTool.Core.Models.Settings;

/// <summary>PRD 6.9.4 <c>config.json</c> 根对象（数据根目录下）。</summary>
public sealed class AppConfiguration
{
    [JsonPropertyName("Theme")]
    public AppThemeOption Theme { get; set; } = AppThemeOption.FollowSystem;

    /// <summary>用户选择的最简数据根路径（与应用实际生效根由锚点协同，可为空表示默认文档目录规则）。</summary>
    [JsonPropertyName("DataPath")]
    public string DataPath { get; set; } = "";

    [JsonPropertyName("AutoBackup")]
    public bool AutoBackup { get; set; }

    /// <summary>可选绝对路径；为空则使用各账户目录下 <see cref="BackupDirectoryRelative"/>。</summary>
    [JsonPropertyName("AutoBackupPath")]
    public string AutoBackupPath { get; set; } = "";

    [JsonPropertyName("BackupRetentionCount")]
    public int BackupRetentionCount { get; set; } = 7;

    [JsonPropertyName("AutoBackupMaxIntervalHours")]
    public int AutoBackupMaxIntervalHours { get; set; } = 24;

    /// <summary>相对当前账户目录的备份子目录名。</summary>
    [JsonPropertyName("BackupDirectoryRelative")]
    public string BackupDirectoryRelative { get; set; } = "Backup";

    /// <summary>UTC，自动/手动备份成功后更新。</summary>
    public DateTime? LastSuccessfulBackupUtc { get; set; }

    [JsonPropertyName("Shortcuts")]
    public Dictionary<string, string> Shortcuts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("LastUpdateTime")]
    public string LastUpdateTime { get; set; } = "";
}

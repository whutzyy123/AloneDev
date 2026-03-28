namespace PMTool.Core.Models.DataManagement;

public enum BackupVerificationStatus
{
    Unknown,
    Success,
    Failed,
}

/// <summary>备份列表项（PRD 6.8.4）。</summary>
public sealed class BackupFileInfo
{
    public required string FileName { get; init; }

    public required string AbsolutePath { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>字节。</summary>
    public long SizeBytes { get; init; }

    public BackupVerificationStatus LastVerification { get; init; }

    public string? VerificationError { get; init; }

    public bool IsPreRestoreBackup =>
        FileName.StartsWith("恢复前_", StringComparison.Ordinal);
}

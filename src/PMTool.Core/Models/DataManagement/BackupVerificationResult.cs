namespace PMTool.Core.Models.DataManagement;

public sealed class BackupVerificationResult
{
    public bool IsOk { get; init; }

    public int UserVersion { get; init; }

    public string? ErrorMessage { get; init; }
}

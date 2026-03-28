namespace PMTool.Core.Models.DataManagement;

public sealed class RestoreResult
{
    public bool Succeeded { get; init; }

    public bool RolledBack { get; init; }

    public string? Message { get; init; }

    public string? PreRestoreBackupPath { get; init; }
}

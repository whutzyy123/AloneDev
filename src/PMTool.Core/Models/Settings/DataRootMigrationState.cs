namespace PMTool.Core.Models.Settings;

/// <summary>中断恢复用状态，存于 LocalAppData。</summary>
public sealed class DataRootMigrationState
{
    public string SourceRoot { get; set; } = "";

    public string TargetRoot { get; set; } = "";

    public MigrationPhase Phase { get; set; }

    public long BytesCopied { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public static string FileName => "migration_state.json";
}

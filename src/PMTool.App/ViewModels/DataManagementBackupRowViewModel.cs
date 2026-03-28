namespace PMTool.App.ViewModels;

public sealed class DataManagementBackupRowViewModel
{
    public required string FileName { get; init; }

    public required string AbsolutePath { get; init; }

    public required string SizeDisplay { get; init; }

    public required string TimeDisplay { get; init; }
}

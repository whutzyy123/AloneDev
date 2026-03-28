using System.Text.Json;
using PMTool.Core.Models.Settings;

namespace PMTool.Infrastructure.Storage;

/// <summary>启动前同步解析，避免 DI 循环。</summary>
public static class DataRootPaths
{
    public static string DefaultDocumentsDataRoot()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "PMProjectTool", "Data");
    }

    public static string LocalAloneDevDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AloneDev");

    public static string AnchorFilePath() => Path.Combine(LocalAloneDevDir(), "config.anchor.json");

    public static string? TryReadAnchorEffectiveRoot()
    {
        try
        {
            var path = AnchorFilePath();
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<AnchorDto>(json);
            if (doc?.EffectiveDataRoot is { Length: > 0 } r)
            {
                return Path.GetFullPath(r);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private sealed class AnchorDto
    {
        public string? EffectiveDataRoot { get; set; }
    }

    public static string MigrationStatePath() =>
        Path.Combine(LocalAloneDevDir(), DataRootMigrationState.FileName);
}

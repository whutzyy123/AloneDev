using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Diagnostics;

public sealed class FileErrorLogger(IDataRootProvider dataRootProvider) : IErrorLogger
{
    private static readonly object Sync = new();

    public void LogException(Exception exception, string? context = null)
    {
        try
        {
            var toolRoot = Path.GetFullPath(Path.Combine(dataRootProvider.GetDataRootPath(), ".."));
            var logsDir = Path.Combine(toolRoot, "Logs");
            _ = Directory.CreateDirectory(logsDir);
            var fileName = $"error-{DateTime.UtcNow:yyyy-MM-dd}.log";
            var path = Path.Combine(logsDir, fileName);
            var line = $"{DateTime.UtcNow:O}\t{context}\t{exception}\n";
            lock (Sync)
            {
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Never throw from logger
        }
    }
}

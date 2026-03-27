namespace PMTool.Core.Abstractions;

/// <summary>Writes application errors to local storage (no network).</summary>
public interface IErrorLogger
{
    void LogException(Exception exception, string? context = null);
}

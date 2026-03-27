namespace PMTool.Core.Abstractions;

/// <summary>Builds SQLite connection strings for the current account database file.</summary>
public interface IDatabaseConnectionFactory
{
    /// <summary>Connection string suitable for <c>Microsoft.Data.Sqlite</c>.</summary>
    string CreateConnectionString();
}

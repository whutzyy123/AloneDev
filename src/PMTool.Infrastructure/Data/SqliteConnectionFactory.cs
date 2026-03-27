using Microsoft.Data.Sqlite;
using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Data;

/// <summary>Placeholder factory; schema migrations will be added with feature work.</summary>
public sealed class SqliteConnectionFactory(ICurrentAccountContext accountContext) : IDatabaseConnectionFactory
{
    public string CreateConnectionString()
    {
        var path = accountContext.GetDatabaseFilePath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        return new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }
}

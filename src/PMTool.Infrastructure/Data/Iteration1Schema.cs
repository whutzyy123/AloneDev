using System.Data.Common;

namespace PMTool.Infrastructure.Data;

internal static class Iteration1Schema
{
    internal static async Task EnsureProbeTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS iteration1_account_probe (
                id TEXT NOT NULL PRIMARY KEY,
                payload TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            """;
        _ = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

using System.Data.Common;

namespace PMTool.Infrastructure.Data;

internal static class DatabaseBootstrap
{
    internal static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await Iteration1Schema.EnsureProbeTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await ProjectsSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await SchemaMigration.ApplyAsync(connection, cancellationToken).ConfigureAwait(false);
    }
}

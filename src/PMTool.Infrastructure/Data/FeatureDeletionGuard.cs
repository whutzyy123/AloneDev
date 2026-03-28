using System.Data.Common;
using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Data;

public sealed class FeatureDeletionGuard(ISqliteConnectionHolder holder) : IFeatureDeletionGuard
{
    public Task<bool> HasBlockingTasksAsync(string featureId, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT EXISTS(
                  SELECT 1 FROM tasks
                  WHERE feature_id = $f AND is_deleted = 0
                );
                """;
            AddParam(cmd, "$f", featureId);
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result is long l ? l != 0 : Convert.ToInt64(result) != 0;
        }, cancellationToken);
    }

    private static void AddParam(DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}

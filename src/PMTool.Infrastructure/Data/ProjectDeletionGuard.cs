using System.Data.Common;
using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Data;

public sealed class ProjectDeletionGuard(ISqliteConnectionHolder holder) : IProjectDeletionGuard
{
    public Task<bool> HasBlockingAssociationsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    EXISTS(SELECT 1 FROM features WHERE project_id = $p AND is_deleted = 0)
                    OR EXISTS(SELECT 1 FROM tasks WHERE project_id = $p AND is_deleted = 0)
                    OR EXISTS(SELECT 1 FROM releases WHERE project_id = $p AND is_deleted = 0)
                    OR EXISTS(SELECT 1 FROM documents WHERE project_id = $p AND relate_type = '项目' AND is_deleted = 0)
                    OR EXISTS(SELECT 1 FROM ideas WHERE linked_project_id = $p AND is_deleted = 0 AND status = '已立项');
                """;
            AddParam(cmd, "$p", projectId);
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

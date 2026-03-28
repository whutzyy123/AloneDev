using System.Data.Common;
using System.Globalization;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;

namespace PMTool.Infrastructure.Data;

public sealed class TaskRepository(ISqliteConnectionHolder holder) : ITaskRepository
{
    public Task<IReadOnlyList<PmTask>> ListByProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    id, project_id, feature_id, name, description, task_type, status, severity,
                    estimated_hours, actual_hours, completed_at, sort_value,
                    created_at, updated_at, is_deleted, row_version
                FROM tasks
                WHERE project_id = $pid AND is_deleted = 0
                ORDER BY name COLLATE NOCASE, id;
                """;
            AddParam(cmd, "$pid", projectId);
            var list = new List<PmTask>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(ReadTask(reader));
            }

            return (IReadOnlyList<PmTask>)list;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<PmTask>> ListAsync(TaskListQuery query, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            var where = "t.feature_id = $fid AND t.is_deleted = 0";
            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                where += " AND (LOWER(t.name) LIKE LOWER($like) OR LOWER(t.description) LIKE LOWER($like))";
            }

            if (query.StatusFilter is { Length: > 0 })
            {
                where += " AND t.status = $st";
            }

            var (orderExpr, orderDir) = query.SortMode switch
            {
                TaskSortMode.Name => ("t.name COLLATE NOCASE", query.SortDescending ? "DESC" : "ASC"),
                TaskSortMode.UpdatedAt => ("t.updated_at", query.SortDescending ? "DESC" : "ASC"),
                _ => ("t.sort_value", "ASC"),
            };

            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                $"""
                 SELECT
                     t.id, t.project_id, t.feature_id, t.name, t.description, t.task_type, t.status, t.severity,
                     t.estimated_hours, t.actual_hours, t.completed_at, t.sort_value,
                     t.created_at, t.updated_at, t.is_deleted, t.row_version
                 FROM tasks t
                 WHERE {where}
                 ORDER BY {orderExpr} {orderDir}, t.id;
                 """;
            AddParam(cmd, "$fid", query.FeatureId);
            if (query.StatusFilter is { Length: > 0 } st)
            {
                AddParam(cmd, "$st", st);
            }

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var t = query.SearchText.Trim();
                AddParam(cmd, "$like", "%" + EscapeLike(t) + "%");
            }

            var list = new List<PmTask>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(ReadTask(reader));
            }

            return (IReadOnlyList<PmTask>)list;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<PmTask>> ListAllActiveAsync(CancellationToken cancellationToken = default) =>
        holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    t.id, t.project_id, t.feature_id, t.name, t.description, t.task_type, t.status, t.severity,
                    t.estimated_hours, t.actual_hours, t.completed_at, t.sort_value,
                    t.created_at, t.updated_at, t.is_deleted, t.row_version
                FROM tasks t
                WHERE t.is_deleted = 0
                ORDER BY t.updated_at DESC, t.id;
                """;
            var list = new List<PmTask>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(ReadTask(reader));
            }

            return (IReadOnlyList<PmTask>)list;
        }, cancellationToken);

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    public Task<PmTask?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    id, project_id, feature_id, name, description, task_type, status, severity,
                    estimated_hours, actual_hours, completed_at, sort_value,
                    created_at, updated_at, is_deleted, row_version
                FROM tasks
                WHERE id = $id AND is_deleted = 0;
                """;
            AddParam(cmd, "$id", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return ReadTask(reader);
        }, cancellationToken);
    }

    public Task InsertAsync(PmTask task, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            var projectId = await ResolveProjectIdAsync(db, task.FeatureId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("特性不存在或已删除。");

            var nextSort = await GetNextSortValueAsync(db, task.FeatureId, ct).ConfigureAwait(false);
            var toInsert = new PmTask
            {
                Id = task.Id,
                ProjectId = projectId,
                FeatureId = task.FeatureId,
                Name = task.Name,
                Description = task.Description,
                TaskType = task.TaskType,
                Status = task.Status,
                Severity = task.Severity,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours,
                CompletedAt = task.CompletedAt,
                SortValue = nextSort,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                IsDeleted = task.IsDeleted,
                RowVersion = task.RowVersion,
            };
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO tasks (
                    id, project_id, feature_id, name, description, task_type, status, severity,
                    estimated_hours, actual_hours, completed_at, sort_value,
                    created_at, updated_at, is_deleted, row_version)
                VALUES (
                    $id, $pid, $fid, $name, $desc, $tt, $st, $sev,
                    $eh, $ah, $ca, $sv,
                    $cr, $up, 0, 1);
                """;
            BindTaskWrite(cmd, toInsert);
            _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    public Task UpdateAsync(PmTask task, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            string? oldStatus = null;
            await using (var sel = db.CreateCommand())
            {
                sel.CommandText = "SELECT status FROM tasks WHERE id = $id AND is_deleted = 0;";
                AddParam(sel, "$id", task.Id);
                await using var r = await sel.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (!await r.ReadAsync(ct).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("任务不存在或已删除。");
                }

                oldStatus = r.GetString(0);
            }

            if (!TaskStatusTransitions.TryValidate(oldStatus, task.Status, out var stErr))
            {
                throw new InvalidOperationException(stErr ?? "状态变更无效。");
            }

            if (!TaskSeverityRules.TryValidate(task.TaskType, task.Severity, out var sevErr))
            {
                throw new InvalidOperationException(sevErr ?? "严重程度无效。");
            }

            var severity = TaskSeverityRules.NormalizeForPersistence(task.TaskType, task.Severity);
            var projectId = await ResolveProjectIdAsync(db, task.FeatureId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("特性不存在或已删除。");

            string? completedAt;
            if (task.Status == TaskStatuses.Done)
            {
                completedAt = oldStatus != TaskStatuses.Done
                    ? (string.IsNullOrEmpty(task.CompletedAt) ? NowStamp() : task.CompletedAt)
                    : task.CompletedAt;
                if (string.IsNullOrEmpty(completedAt))
                {
                    completedAt = NowStamp();
                }
            }
            else
            {
                completedAt = null;
            }

            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE tasks
                SET project_id = $pid,
                    feature_id = $fid,
                    name = $name,
                    description = $desc,
                    task_type = $tt,
                    status = $st,
                    severity = $sev,
                    estimated_hours = $eh,
                    actual_hours = $ah,
                    completed_at = $ca,
                    updated_at = $up,
                    row_version = row_version + 1
                WHERE id = $id AND is_deleted = 0;
                """;
            AddParam(cmd, "$id", task.Id);
            AddParam(cmd, "$pid", projectId);
            AddParam(cmd, "$fid", task.FeatureId);
            AddParam(cmd, "$name", task.Name);
            AddParam(cmd, "$desc", task.Description);
            AddParam(cmd, "$tt", task.TaskType);
            AddParam(cmd, "$st", task.Status);
            AddParam(cmd, "$sev", severity ?? (object)DBNull.Value);
            AddParam(cmd, "$eh", task.EstimatedHours);
            AddParam(cmd, "$ah", task.ActualHours);
            AddParam(cmd, "$ca", completedAt ?? (object)DBNull.Value);
            AddParam(cmd, "$up", task.UpdatedAt);
            var n = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (n == 0)
            {
                throw new InvalidOperationException("任务不存在或已删除。");
            }

            return 0;
        }, cancellationToken);
    }

    public Task SoftDeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            var now = NowStamp();
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE tasks
                SET is_deleted = 1,
                    updated_at = $ua,
                    row_version = row_version + 1
                WHERE id = $id AND is_deleted = 0;
                """;
            AddParam(cmd, "$id", id);
            AddParam(cmd, "$ua", now);
            _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    public Task MoveWithinFeatureAsync(string taskId, int direction, CancellationToken cancellationToken = default)
    {
        if (direction is not (-1) and not 1)
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var tx = await db.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                string? featureId = null;
                await using (var sel = db.CreateCommand())
                {
                    sel.Transaction = tx;
                    sel.CommandText = "SELECT feature_id FROM tasks WHERE id = $id AND is_deleted = 0;";
                    AddParam(sel, "$id", taskId);
                    var o = await sel.ExecuteScalarAsync(ct).ConfigureAwait(false);
                    featureId = o as string;
                    if (string.IsNullOrEmpty(featureId))
                    {
                        throw new InvalidOperationException("任务不存在。");
                    }
                }

                var ids = new List<string>();
                await using (var ord = db.CreateCommand())
                {
                    ord.Transaction = tx;
                    ord.CommandText =
                        "SELECT id FROM tasks WHERE feature_id = $fid AND is_deleted = 0 ORDER BY sort_value ASC, id;";
                    AddParam(ord, "$fid", featureId);
                    await using var r = await ord.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    while (await r.ReadAsync(ct).ConfigureAwait(false))
                    {
                        ids.Add(r.GetString(0));
                    }
                }

                var idx = ids.IndexOf(taskId);
                if (idx < 0)
                {
                    throw new InvalidOperationException("任务不在列表中。");
                }

                var newIdx = idx + direction;
                if (newIdx < 0 || newIdx >= ids.Count)
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return 0;
                }

                (ids[idx], ids[newIdx]) = (ids[newIdx], ids[idx]);

                var now = NowStamp();
                for (var i = 0; i < ids.Count; i++)
                {
                    await using var up = db.CreateCommand();
                    up.Transaction = tx;
                    up.CommandText =
                        "UPDATE tasks SET sort_value = $sv, updated_at = $ua, row_version = row_version + 1 WHERE id = $id AND is_deleted = 0;";
                    AddParam(up, "$sv", i);
                    AddParam(up, "$ua", now);
                    AddParam(up, "$id", ids[i]);
                    _ = await up.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                await tx.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }

            return 0;
        }, cancellationToken);
    }

    public Task<FeatureTaskProgress> GetFeatureProgressAsync(string featureId, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    COALESCE(SUM(CASE WHEN status = $done THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN status != $cancel THEN 1 ELSE 0 END), 0)
                FROM tasks
                WHERE feature_id = $fid AND is_deleted = 0;
                """;
            AddParam(cmd, "$fid", featureId);
            AddParam(cmd, "$done", TaskStatuses.Done);
            AddParam(cmd, "$cancel", TaskStatuses.Cancelled);
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await r.ReadAsync(ct).ConfigureAwait(false))
            {
                return new FeatureTaskProgress(0, 0);
            }

            var completed = (int)r.GetInt64(0);
            var total = (int)r.GetInt64(1);
            return new FeatureTaskProgress(completed, total);
        }, cancellationToken);
    }

    private static async Task<string?> ResolveProjectIdAsync(DbConnection db, string featureId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT project_id FROM features WHERE id = $id AND is_deleted = 0;";
        AddParam(cmd, "$id", featureId);
        var o = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return o as string;
    }

    private static async Task<int> GetNextSortValueAsync(DbConnection db, string featureId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            "SELECT COALESCE(MAX(sort_value), -1) FROM tasks WHERE feature_id = $fid AND is_deleted = 0;";
        AddParam(cmd, "$fid", featureId);
        var o = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var max = o is DBNull or null ? -1 : Convert.ToInt32(o);
        return max + 1;
    }

    private static PmTask ReadTask(DbDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            ProjectId = reader.GetString(1),
            FeatureId = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Name = reader.GetString(3),
            Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            TaskType = reader.GetString(5),
            Status = reader.GetString(6),
            Severity = reader.IsDBNull(7) ? null : reader.GetString(7),
            EstimatedHours = reader.GetDouble(8),
            ActualHours = reader.GetDouble(9),
            CompletedAt = reader.IsDBNull(10) ? null : reader.GetString(10),
            SortValue = reader.GetInt32(11),
            CreatedAt = reader.GetString(12),
            UpdatedAt = reader.GetString(13),
            IsDeleted = reader.GetInt32(14) != 0,
            RowVersion = reader.GetInt64(15),
        };

    private static void BindTaskWrite(DbCommand cmd, PmTask task)
    {
        var severity = TaskSeverityRules.NormalizeForPersistence(task.TaskType, task.Severity);
        AddParam(cmd, "$id", task.Id);
        AddParam(cmd, "$pid", task.ProjectId);
        AddParam(cmd, "$fid", task.FeatureId);
        AddParam(cmd, "$name", task.Name);
        AddParam(cmd, "$desc", task.Description);
        AddParam(cmd, "$tt", task.TaskType);
        AddParam(cmd, "$st", task.Status);
        AddParam(cmd, "$sev", severity ?? (object)DBNull.Value);
        AddParam(cmd, "$eh", task.EstimatedHours);
        AddParam(cmd, "$ah", task.ActualHours);
        AddParam(cmd, "$ca", task.CompletedAt ?? (object)DBNull.Value);
        AddParam(cmd, "$sv", task.SortValue);
        AddParam(cmd, "$cr", task.CreatedAt);
        AddParam(cmd, "$up", task.UpdatedAt);
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static string NowStamp() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}

using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;

namespace PMTool.Infrastructure.Data;

public sealed class ReleaseRepository(
    ISqliteConnectionHolder holder,
    IFeatureRepository featureRepository,
    ITaskRepository taskRepository) : IReleaseRepository
{
    public Task<IReadOnlyList<Release>> ListAsync(ReleaseListQuery query, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            var where = "r.project_id = $pid AND r.is_deleted = 0";
            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                where += " AND (LOWER(r.name) LIKE LOWER($like) OR LOWER(r.description) LIKE LOWER($like))";
            }

            if (query.StatusFilter is { Length: > 0 })
            {
                where += " AND r.status = $st";
            }

            var orderCol = query.SortField switch
            {
                ReleaseSortField.Name => "r.name COLLATE NOCASE",
                ReleaseSortField.StartAt => "r.start_at",
                _ => "r.updated_at",
            };
            var orderDir = query.SortDescending ? "DESC" : "ASC";

            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                $"""
                 SELECT
                     r.id, r.project_id, r.name, r.description, r.start_at, r.end_at, r.status,
                     r.created_at, r.updated_at, r.is_deleted, r.row_version
                 FROM releases r
                 WHERE {where}
                 ORDER BY {orderCol} {orderDir}, r.id;
                 """;

            AddParam(cmd, "$pid", query.ProjectId);
            if (query.StatusFilter is { Length: > 0 } st)
            {
                AddParam(cmd, "$st", st);
            }

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var t = query.SearchText.Trim();
                AddParam(cmd, "$like", "%" + EscapeLike(t) + "%");
            }

            var list = new List<Release>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(ReadRelease(reader));
            }

            return (IReadOnlyList<Release>)list;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<Release>> ListAllActiveAsync(CancellationToken cancellationToken = default) =>
        holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    r.id, r.project_id, r.name, r.description, r.start_at, r.end_at, r.status,
                    r.created_at, r.updated_at, r.is_deleted, r.row_version
                FROM releases r
                WHERE r.is_deleted = 0
                ORDER BY r.updated_at DESC, r.id;
                """;
            var list = new List<Release>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(ReadRelease(reader));
            }

            return (IReadOnlyList<Release>)list;
        }, cancellationToken);

    public Task<Release?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    id, project_id, name, description, start_at, end_at, status,
                    created_at, updated_at, is_deleted, row_version
                FROM releases
                WHERE id = $id AND is_deleted = 0;
                """;
            AddParam(cmd, "$id", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return ReadRelease(reader);
        }, cancellationToken);
    }

    public Task InsertAsync(Release release, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO releases (
                    id, project_id, name, description, start_at, end_at, status,
                    created_at, updated_at, is_deleted, row_version)
                VALUES (
                    $id, $pid, $name, $desc, $start, $end, $status,
                    $ca, $ua, 0, 1);
                """;
            BindReleaseWrite(cmd, release);
            try
            {
                _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067)
            {
                throw new InvalidOperationException("数据约束冲突。", ex);
            }

            return 0;
        }, cancellationToken);
    }

    public Task UpdateAsync(Release release, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            Release? existing = null;
            await using (var sel = db.CreateCommand())
            {
                sel.CommandText =
                    """
                    SELECT
                        id, project_id, name, description, start_at, end_at, status,
                        created_at, updated_at, is_deleted, row_version
                    FROM releases
                    WHERE id = $id AND is_deleted = 0;
                    """;
                AddParam(sel, "$id", release.Id);
                await using var rd = await sel.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (!await rd.ReadAsync(ct).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("版本不存在或已删除。");
                }

                existing = ReadRelease(rd);
            }

            if (existing is null)
            {
                throw new InvalidOperationException("版本不存在或已删除。");
            }

            if (ReleaseStatusTransitions.IsTerminal(existing.Status))
            {
                throw new InvalidOperationException("已结束或已取消的版本不可修改。");
            }

            if (existing.Status == ReleaseStatuses.InProgress)
            {
                if (release.Name != existing.Name
                    || release.Description != existing.Description
                    || release.StartAt != existing.StartAt
                    || release.EndAt != existing.EndAt
                    || release.ProjectId != existing.ProjectId)
                {
                    throw new InvalidOperationException("进行中的版本仅可变更状态为已结束或已取消。");
                }

                if (!ReleaseStatusTransitions.TryValidate(existing.Status, release.Status, out var errProg))
                {
                    throw new InvalidOperationException(errProg ?? "状态变更无效。");
                }
            }
            else if (existing.Status == ReleaseStatuses.NotStarted)
            {
                if (!ReleaseStatusTransitions.TryValidate(existing.Status, release.Status, out var errNs))
                {
                    throw new InvalidOperationException(errNs ?? "状态变更无效。");
                }
            }

            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE releases
                SET name = $name,
                    description = $desc,
                    start_at = $start,
                    end_at = $end,
                    status = $status,
                    updated_at = $ua,
                    row_version = row_version + 1
                WHERE id = $id AND is_deleted = 0 AND row_version = $rv;
                """;
            AddParam(cmd, "$id", release.Id);
            AddParam(cmd, "$name", release.Name);
            AddParam(cmd, "$desc", release.Description);
            AddParam(cmd, "$start", release.StartAt);
            AddParam(cmd, "$end", release.EndAt);
            AddParam(cmd, "$status", release.Status);
            AddParam(cmd, "$ua", release.UpdatedAt);
            AddParam(cmd, "$rv", existing.RowVersion);
            var n = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (n == 0)
            {
                throw new InvalidOperationException("版本已被他人修改或不存在，请刷新后重试。");
            }

            return 0;
        }, cancellationToken);
    }

    public Task SoftDeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            string? status = null;
            await using (var sel = db.CreateCommand())
            {
                sel.CommandText = "SELECT status FROM releases WHERE id = $id AND is_deleted = 0;";
                AddParam(sel, "$id", id);
                var o = await sel.ExecuteScalarAsync(ct).ConfigureAwait(false);
                if (o is null or DBNull)
                {
                    throw new InvalidOperationException("版本不存在或已删除。");
                }

                status = o.ToString();
            }

            if (!ReleaseStatusTransitions.CanEditOrDelete(status ?? ""))
            {
                throw new InvalidOperationException("仅未开始版本支持删除。");
            }

            await using var tx = await db.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                await using (var delRel = db.CreateCommand())
                {
                    delRel.CommandText = "DELETE FROM release_relations WHERE release_id = $id;";
                    AddParam(delRel, "$id", id);
                    _ = await delRel.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                var now = NowStamp();
                await using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText =
                        """
                        UPDATE releases
                        SET is_deleted = 1,
                            updated_at = $ua,
                            row_version = row_version + 1
                        WHERE id = $id AND is_deleted = 0;
                        """;
                    AddParam(cmd, "$id", id);
                    AddParam(cmd, "$ua", now);
                    _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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

    public Task<IReadOnlyList<ReleaseRelationRow>> ListRelationsAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    rr.id,
                    rr.target_type,
                    rr.target_id,
                    COALESCE(
                        CASE WHEN rr.target_type = 'feature' THEN f.name END,
                        CASE WHEN rr.target_type = 'task' THEN t.name END,
                        '') AS title
                FROM release_relations rr
                LEFT JOIN features f ON rr.target_type = 'feature' AND f.id = rr.target_id AND f.is_deleted = 0
                LEFT JOIN tasks t ON rr.target_type = 'task' AND t.id = rr.target_id AND t.is_deleted = 0
                WHERE rr.release_id = $rid
                ORDER BY rr.target_type, title COLLATE NOCASE;
                """;
            AddParam(cmd, "$rid", releaseId);
            var list = new List<ReleaseRelationRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var title = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                list.Add(new ReleaseRelationRow
                {
                    RelationId = reader.GetString(0),
                    TargetType = reader.GetString(1),
                    TargetId = reader.GetString(2),
                    DisplayName = string.IsNullOrEmpty(title) ? reader.GetString(2) : title,
                });
            }

            return (IReadOnlyList<ReleaseRelationRow>)list;
        }, cancellationToken);
    }

    public async Task AddRelationAsync(string releaseId, string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        var rel = await GetByIdAsync(releaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("版本不存在或已删除。");

        if (rel.Status != ReleaseStatuses.NotStarted && rel.Status != ReleaseStatuses.InProgress)
        {
            throw new InvalidOperationException("已结束或已取消的版本不可添加关联。");
        }

        if (targetType == ReleaseRelationTarget.Feature)
        {
            var f = await featureRepository.GetByIdAsync(targetId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("模块不存在。");
            if (f.ProjectId != rel.ProjectId)
            {
                throw new InvalidOperationException("仅可关联同一项目下的模块。");
            }
        }
        else if (targetType == ReleaseRelationTarget.Task)
        {
            var t = await taskRepository.GetByIdAsync(targetId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("任务不存在。");
            if (t.ProjectId != rel.ProjectId)
            {
                throw new InvalidOperationException("仅可关联同一项目下的任务。");
            }
        }
        else
        {
            throw new InvalidOperationException("未知的关联类型。");
        }

        await holder.UseConnectionAsync(async (db, ct) =>
        {
            var now = NowStamp();
            var rid = Guid.NewGuid().ToString("D");
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                INSERT OR IGNORE INTO release_relations (id, release_id, target_type, target_id, created_at)
                VALUES ($id, $rel, $tt, $tid, $ca);
                """;
            AddParam(cmd, "$id", rid);
            AddParam(cmd, "$rel", releaseId);
            AddParam(cmd, "$tt", targetType);
            AddParam(cmd, "$tid", targetId);
            AddParam(cmd, "$ca", now);
            _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task RemoveRelationAsync(string releaseId, string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                "DELETE FROM release_relations WHERE release_id = $r AND target_type = $tt AND target_id = $tid;";
            AddParam(cmd, "$r", releaseId);
            AddParam(cmd, "$tt", targetType);
            AddParam(cmd, "$tid", targetId);
            _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    public Task<ReleaseProgressStats> GetProgressAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    IFNULL(SUM(CASE
                        WHEN rr.target_type = 'feature' AND f.id IS NOT NULL AND f.is_deleted = 0 THEN 1
                        ELSE 0 END), 0),
                    IFNULL(SUM(CASE
                        WHEN rr.target_type = 'feature' AND f.id IS NOT NULL AND f.is_deleted = 0
                             AND f.status IN ($doneF1, $released) THEN 1
                        ELSE 0 END), 0),
                    IFNULL(SUM(CASE
                        WHEN rr.target_type = 'task' AND t.id IS NOT NULL AND t.is_deleted = 0 THEN 1
                        ELSE 0 END), 0),
                    IFNULL(SUM(CASE
                        WHEN rr.target_type = 'task' AND t.id IS NOT NULL AND t.is_deleted = 0
                             AND t.status = $doneT THEN 1
                        ELSE 0 END), 0)
                FROM release_relations rr
                LEFT JOIN features f ON rr.target_type = 'feature' AND f.id = rr.target_id
                LEFT JOIN tasks t ON rr.target_type = 'task' AND t.id = rr.target_id
                WHERE rr.release_id = $rid;
                """;
            AddParam(cmd, "$rid", releaseId);
            AddParam(cmd, "$doneF1", FeatureStatuses.Done);
            AddParam(cmd, "$released", FeatureStatuses.Released);
            AddParam(cmd, "$doneT", TaskStatuses.Done);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return new ReleaseProgressStats(0, 0, 0, 0, 0);
            }

            var F = Convert.ToInt32(reader.GetInt64(0));
            var f = Convert.ToInt32(reader.GetInt64(1));
            var T = Convert.ToInt32(reader.GetInt64(2));
            var t = Convert.ToInt32(reader.GetInt64(3));
            var pct = ReleaseProgressStats.ComputePercent(F, f, T, t);
            return new ReleaseProgressStats(F, f, T, t, pct);
        }, cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, ReleaseProgressStats>> GetProgressBatchAsync(
        IReadOnlyList<string> releaseIds,
        CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            var dict = new Dictionary<string, ReleaseProgressStats>(StringComparer.Ordinal);
            if (releaseIds.Count == 0)
            {
                return dict;
            }

            foreach (var id in releaseIds)
            {
                var one = await GetProgressForConnectionAsync(db, id, ct).ConfigureAwait(false);
                dict[id] = one;
            }

            return (IReadOnlyDictionary<string, ReleaseProgressStats>)dict;
        }, cancellationToken);
    }

    private static async Task<ReleaseProgressStats> GetProgressForConnectionAsync(
        DbConnection db,
        string releaseId,
        CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            SELECT
                IFNULL(SUM(CASE
                    WHEN rr.target_type = 'feature' AND f.id IS NOT NULL AND f.is_deleted = 0 THEN 1
                    ELSE 0 END), 0),
                IFNULL(SUM(CASE
                    WHEN rr.target_type = 'feature' AND f.id IS NOT NULL AND f.is_deleted = 0
                         AND f.status IN ($doneF1, $released) THEN 1
                    ELSE 0 END), 0),
                IFNULL(SUM(CASE
                    WHEN rr.target_type = 'task' AND t.id IS NOT NULL AND t.is_deleted = 0 THEN 1
                    ELSE 0 END), 0),
                IFNULL(SUM(CASE
                    WHEN rr.target_type = 'task' AND t.id IS NOT NULL AND t.is_deleted = 0
                         AND t.status = $doneT THEN 1
                    ELSE 0 END), 0)
            FROM release_relations rr
            LEFT JOIN features f ON rr.target_type = 'feature' AND f.id = rr.target_id
            LEFT JOIN tasks t ON rr.target_type = 'task' AND t.id = rr.target_id
            WHERE rr.release_id = $rid;
            """;
        AddParam(cmd, "$rid", releaseId);
        AddParam(cmd, "$doneF1", FeatureStatuses.Done);
        AddParam(cmd, "$released", FeatureStatuses.Released);
        AddParam(cmd, "$doneT", TaskStatuses.Done);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new ReleaseProgressStats(0, 0, 0, 0, 0);
        }

        var F = Convert.ToInt32(reader.GetInt64(0));
        var f = Convert.ToInt32(reader.GetInt64(1));
        var T = Convert.ToInt32(reader.GetInt64(2));
        var tC = Convert.ToInt32(reader.GetInt64(3));
        var pct = ReleaseProgressStats.ComputePercent(F, f, T, tC);
        return new ReleaseProgressStats(F, f, T, tC, pct);
    }

    private static Release ReadRelease(DbDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            ProjectId = reader.GetString(1),
            Name = reader.GetString(2),
            Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            StartAt = reader.GetString(4),
            EndAt = reader.GetString(5),
            Status = reader.GetString(6),
            CreatedAt = reader.GetString(7),
            UpdatedAt = reader.GetString(8),
            IsDeleted = reader.GetInt32(9) != 0,
            RowVersion = reader.GetInt64(10),
        };

    private static void BindReleaseWrite(DbCommand cmd, Release release)
    {
        AddParam(cmd, "$id", release.Id);
        AddParam(cmd, "$pid", release.ProjectId);
        AddParam(cmd, "$name", release.Name);
        AddParam(cmd, "$desc", release.Description);
        AddParam(cmd, "$start", release.StartAt);
        AddParam(cmd, "$end", release.EndAt);
        AddParam(cmd, "$status", release.Status);
        AddParam(cmd, "$ca", release.CreatedAt);
        AddParam(cmd, "$ua", release.UpdatedAt);
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private static string NowStamp() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}

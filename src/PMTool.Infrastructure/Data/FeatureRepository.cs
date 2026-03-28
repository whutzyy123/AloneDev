using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;

namespace PMTool.Infrastructure.Data;

public sealed class FeatureRepository(ISqliteConnectionHolder holder) : IFeatureRepository
{
    public Task<IReadOnlyList<Feature>> ListAsync(FeatureListQuery query, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            var where = "f.project_id = $pid AND f.is_deleted = 0";
            AddParamPlaceholder(query, ref where, out var searchLike);

            if (query.StatusFilter is { Length: > 0 })
            {
                where += " AND f.status = $st";
            }

            var orderCol = query.SortField switch
            {
                FeatureSortField.Name => "f.name COLLATE NOCASE",
                FeatureSortField.CreatedAt => "f.created_at",
                _ => "f.updated_at",
            };
            var orderDir = query.SortDescending ? "DESC" : "ASC";

            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                $"""
                 SELECT
                     f.id, f.project_id, f.name, f.description, f.status, f.priority,
                     f.acceptance_criteria, f.tech_stack, f.notes, f.due_date, f.attachments_placeholder,
                     f.created_at, f.updated_at, f.is_deleted, f.row_version
                 FROM features f
                 WHERE {where}
                 ORDER BY {orderCol} {orderDir};
                 """;

            AddParam(cmd, "$pid", query.ProjectId);
            if (query.StatusFilter is { Length: > 0 } st)
            {
                AddParam(cmd, "$st", st);
            }

            if (searchLike is not null)
            {
                AddParam(cmd, "$like", searchLike);
            }

            var list = new List<Feature>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(ReadFeature(reader));
            }

            return (IReadOnlyList<Feature>)list;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<Feature>> ListAllActiveAsync(CancellationToken cancellationToken = default) =>
        holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    f.id, f.project_id, f.name, f.description, f.status, f.priority,
                    f.acceptance_criteria, f.tech_stack, f.notes, f.due_date, f.attachments_placeholder,
                    f.created_at, f.updated_at, f.is_deleted, f.row_version
                FROM features f
                WHERE f.is_deleted = 0
                ORDER BY f.updated_at DESC, f.id;
                """;
            var list = new List<Feature>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(ReadFeature(reader));
            }

            return (IReadOnlyList<Feature>)list;
        }, cancellationToken);

    private static void AddParamPlaceholder(FeatureListQuery query, ref string where, out string? searchLike)
    {
        searchLike = null;
        if (string.IsNullOrWhiteSpace(query.SearchText))
        {
            return;
        }

        where += " AND (LOWER(f.name) LIKE LOWER($like) OR LOWER(f.description) LIKE LOWER($like) OR LOWER(f.notes) LIKE LOWER($like))";
        var t = query.SearchText.Trim();
        searchLike = "%" + EscapeLike(t) + "%";
    }

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    public Task<Feature?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    id, project_id, name, description, status, priority,
                    acceptance_criteria, tech_stack, notes, due_date, attachments_placeholder,
                    created_at, updated_at, is_deleted, row_version
                FROM features
                WHERE id = $id AND is_deleted = 0;
                """;
            AddParam(cmd, "$id", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return ReadFeature(reader);
        }, cancellationToken);
    }

    public Task InsertAsync(Feature feature, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO features (
                    id, project_id, name, description, status, priority,
                    acceptance_criteria, tech_stack, notes, due_date, attachments_placeholder,
                    created_at, updated_at, is_deleted, row_version)
                VALUES (
                    $id, $pid, $name, $desc, $status, $pri,
                    $ac, $ts, $notes, $due, $att,
                    $ca, $ua, 0, 1);
                """;
            BindFeatureWrite(cmd, feature);
            try
            {
                _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067 || ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("同一项目下已存在同名特性，请修改名称。", ex);
            }

            return 0;
        }, cancellationToken);
    }

    public Task UpdateAsync(Feature feature, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            string? oldStatus = null;
            await using (var sel = db.CreateCommand())
            {
                sel.CommandText = "SELECT status FROM features WHERE id = $id AND is_deleted = 0;";
                AddParam(sel, "$id", feature.Id);
                var o = await sel.ExecuteScalarAsync(ct).ConfigureAwait(false);
                if (o is null or DBNull)
                {
                    throw new InvalidOperationException("特性不存在或已删除。");
                }

                oldStatus = o.ToString();
            }

            if (!FeatureStatusTransitions.TryValidate(oldStatus, feature.Status, out var err))
            {
                throw new InvalidOperationException(err ?? "状态变更无效。");
            }

            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE features
                SET name = $name,
                    description = $desc,
                    status = $status,
                    priority = $pri,
                    acceptance_criteria = $ac,
                    tech_stack = $ts,
                    notes = $notes,
                    due_date = $due,
                    attachments_placeholder = $att,
                    updated_at = $ua,
                    row_version = row_version + 1
                WHERE id = $id AND is_deleted = 0;
                """;
            AddParam(cmd, "$id", feature.Id);
            AddParam(cmd, "$name", feature.Name);
            AddParam(cmd, "$desc", feature.Description);
            AddParam(cmd, "$status", feature.Status);
            AddParam(cmd, "$pri", feature.Priority);
            AddParam(cmd, "$ac", feature.AcceptanceCriteria);
            AddParam(cmd, "$ts", feature.TechStack);
            AddParam(cmd, "$notes", feature.Notes);
            AddParam(cmd, "$due", feature.DueDate ?? (object)DBNull.Value);
            AddParam(cmd, "$att", feature.AttachmentsPlaceholder ?? (object)DBNull.Value);
            AddParam(cmd, "$ua", feature.UpdatedAt);
            try
            {
                var n = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new InvalidOperationException("特性不存在或已删除。");
                }
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067 || ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("同一项目下已存在同名特性，请修改名称。", ex);
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
                UPDATE features
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

    private static Feature ReadFeature(DbDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            ProjectId = reader.GetString(1),
            Name = reader.GetString(2),
            Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Status = reader.GetString(4),
            Priority = reader.GetInt32(5),
            AcceptanceCriteria = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            TechStack = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            Notes = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
            DueDate = reader.IsDBNull(9) ? null : reader.GetString(9),
            AttachmentsPlaceholder = reader.IsDBNull(10) ? null : reader.GetString(10),
            CreatedAt = reader.GetString(11),
            UpdatedAt = reader.GetString(12),
            IsDeleted = reader.GetInt32(13) != 0,
            RowVersion = reader.GetInt64(14),
        };

    private static void BindFeatureWrite(DbCommand cmd, Feature feature)
    {
        AddParam(cmd, "$id", feature.Id);
        AddParam(cmd, "$pid", feature.ProjectId);
        AddParam(cmd, "$name", feature.Name);
        AddParam(cmd, "$desc", feature.Description);
        AddParam(cmd, "$status", feature.Status);
        AddParam(cmd, "$pri", feature.Priority);
        AddParam(cmd, "$ac", feature.AcceptanceCriteria);
        AddParam(cmd, "$ts", feature.TechStack);
        AddParam(cmd, "$notes", feature.Notes);
        AddParam(cmd, "$due", feature.DueDate ?? (object)DBNull.Value);
        AddParam(cmd, "$att", feature.AttachmentsPlaceholder ?? (object)DBNull.Value);
        AddParam(cmd, "$ca", feature.CreatedAt);
        AddParam(cmd, "$ua", feature.UpdatedAt);
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

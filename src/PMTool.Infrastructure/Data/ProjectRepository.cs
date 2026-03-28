using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;

namespace PMTool.Infrastructure.Data;

public sealed class ProjectRepository(ISqliteConnectionHolder holder) : IProjectRepository
{
    public Task<IReadOnlyList<ProjectListItem>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            var where = "p.is_deleted = 0";
            if (query.StatusFilter is { Length: > 0 })
            {
                where += " AND p.status = $status";
            }

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                where += " AND (LOWER(p.name) LIKE LOWER($like) OR LOWER(p.description) LIKE LOWER($like) OR LOWER(p.tech_stack) LIKE LOWER($like))";
            }

            var orderCol = query.SortField switch
            {
                ProjectSortField.Name => "p.name COLLATE NOCASE",
                ProjectSortField.CreatedAt => "p.created_at",
                _ => "p.updated_at",
            };
            var orderDir = query.SortDescending ? "DESC" : "ASC";

            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                $"""
                 SELECT
                     p.id, p.name, p.description, p.status, p.category, p.tech_stack, p.created_at, p.updated_at, p.is_deleted, p.row_version, p.local_git_root,
                     (SELECT COUNT(*) FROM features f WHERE f.project_id = p.id AND f.is_deleted = 0),
                     (SELECT COUNT(*) FROM tasks t WHERE t.project_id = p.id AND t.is_deleted = 0),
                     (SELECT COUNT(*) FROM releases r WHERE r.project_id = p.id AND r.is_deleted = 0),
                     (SELECT COUNT(*) FROM documents d WHERE d.project_id = p.id AND d.relate_type = '项目' AND d.is_deleted = 0),
                     (SELECT COUNT(*) FROM ideas i WHERE i.linked_project_id = p.id AND i.is_deleted = 0)
                 FROM projects p
                 WHERE {where}
                 ORDER BY {orderCol} {orderDir};
                 """;

            if (query.StatusFilter is { Length: > 0 } st2)
            {
                AddParam(cmd, "$status", st2);
            }

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var t = query.SearchText.Trim();
                AddParam(cmd, "$like", "%" + EscapeLike(t) + "%");
            }

            var list = new List<ProjectListItem>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new ProjectListItem
                {
                    Project = ReadProject(reader, 0),
                    FeatureCount = reader.GetInt32(11),
                    TaskCount = reader.GetInt32(12),
                    ReleaseCount = reader.GetInt32(13),
                    DocumentCount = reader.GetInt32(14),
                    LinkedIdeaCount = reader.GetInt32(15),
                });
            }

            return (IReadOnlyList<ProjectListItem>)list;
        }, cancellationToken);
    }

    private static string EscapeLike(string s) => s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    public Task<Project?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, name, description, status, category, tech_stack, created_at, updated_at, is_deleted, row_version, local_git_root
                FROM projects
                WHERE id = $id AND is_deleted = 0;
                """;
            AddParam(cmd, "$id", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return ReadProject(reader, 0);
        }, cancellationToken);
    }

    public Task InsertAsync(Project project, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO projects (id, name, description, status, category, tech_stack, created_at, updated_at, is_deleted, row_version, local_git_root)
                VALUES ($id, $name, $desc, $status, $cat, $tstack, $ca, $ua, 0, 1, $git);
                """;
            AddParam(cmd, "$id", project.Id);
            AddParam(cmd, "$name", project.Name);
            AddParam(cmd, "$desc", project.Description);
            AddParam(cmd, "$status", project.Status);
            AddParam(cmd, "$cat", project.Category ?? (object)DBNull.Value);
            AddParam(cmd, "$tstack", project.TechStack ?? string.Empty);
            AddParam(cmd, "$ca", project.CreatedAt);
            AddParam(cmd, "$ua", project.UpdatedAt);
            AddParam(cmd, "$git", project.LocalGitRoot ?? (object)DBNull.Value);
            try
            {
                _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067 || ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("该项目名称在当前状态下已存在，请修改。", ex);
            }

            return 0;
        }, cancellationToken);
    }

    public Task UpdateCoreAsync(Project project, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE projects
                SET name = $name,
                    description = $desc,
                    status = $status,
                    category = $cat,
                    tech_stack = $tstack,
                    local_git_root = $git,
                    updated_at = $ua,
                    row_version = row_version + 1
                WHERE id = $id AND is_deleted = 0;
                """;
            AddParam(cmd, "$id", project.Id);
            AddParam(cmd, "$name", project.Name);
            AddParam(cmd, "$desc", project.Description);
            AddParam(cmd, "$status", project.Status);
            AddParam(cmd, "$cat", project.Category ?? (object)DBNull.Value);
            AddParam(cmd, "$tstack", project.TechStack ?? string.Empty);
            AddParam(cmd, "$git", project.LocalGitRoot ?? (object)DBNull.Value);
            AddParam(cmd, "$ua", project.UpdatedAt);
            try
            {
                var n = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new InvalidOperationException("项目不存在或已删除。");
                }
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067 || ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("该项目名称在当前状态下已存在，请修改。", ex);
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
                UPDATE projects
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

    public Task<bool> ExistsNameInStatusAsync(string name, string status, string? excludeProjectId, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            var exClause = string.IsNullOrEmpty(excludeProjectId) ? "1=1" : "id != $ex";
            cmd.CommandText =
                $"""
                SELECT EXISTS(
                  SELECT 1 FROM projects
                  WHERE name = $n COLLATE NOCASE AND status = $s AND is_deleted = 0 AND {exClause}
                );
                """;
            AddParam(cmd, "$n", name);
            AddParam(cmd, "$s", status);
            if (!string.IsNullOrEmpty(excludeProjectId))
            {
                AddParam(cmd, "$ex", excludeProjectId);
            }

            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false)!;
            return result is long l ? l != 0 : Convert.ToInt64(result) != 0;
        }, cancellationToken);
    }

    private static Project ReadProject(DbDataReader reader, int offset = 0)
    {
        return new Project
        {
            Id = reader.GetString(offset),
            Name = reader.GetString(offset + 1),
            Description = reader.IsDBNull(offset + 2) ? string.Empty : reader.GetString(offset + 2),
            Status = reader.GetString(offset + 3),
            Category = reader.IsDBNull(offset + 4) ? null : reader.GetString(offset + 4),
            TechStack = reader.IsDBNull(offset + 5) ? string.Empty : reader.GetString(offset + 5),
            CreatedAt = reader.GetString(offset + 6),
            UpdatedAt = reader.GetString(offset + 7),
            IsDeleted = reader.GetInt32(offset + 8) != 0,
            RowVersion = reader.GetInt64(offset + 9),
            LocalGitRoot = reader.IsDBNull(offset + 10) ? null : reader.GetString(offset + 10),
        };
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

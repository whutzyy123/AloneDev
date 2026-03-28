using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;
using PMTool.Core.Validation;

namespace PMTool.Infrastructure.Data;

public sealed class IdeaRepository(ISqliteConnectionHolder holder, IDocumentRepository documentRepository) : IIdeaRepository
{
    public Task<IReadOnlyList<Idea>> ListAsync(IdeaListQuery query, CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            var where = "i.is_deleted = 0";
            string? searchLike = null;

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                where += " AND (LOWER(i.title) LIKE LOWER($like) OR LOWER(i.description) LIKE LOWER($like) OR LOWER(i.tech_stack) LIKE LOWER($like))";
                searchLike = "%" + EscapeLike(query.SearchText.Trim()) + "%";
            }

            if (query.StatusFilter is { Length: > 0 })
            {
                where += " AND i.status = $st";
            }

            if (query.PriorityFilter is { Length: > 0 })
            {
                where += " AND i.priority = $pri";
            }

            var orderCol = query.SortField switch
            {
                IdeaSortField.Title => "i.title COLLATE NOCASE",
                IdeaSortField.Status => "i.status COLLATE NOCASE",
                _ => "i.updated_at",
            };
            var orderDir = query.SortDescending ? "DESC" : "ASC";

            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                $"""
                 SELECT
                     i.id, i.title, i.description, i.tech_stack, i.status, i.priority, i.linked_project_id,
                     i.created_at, i.updated_at, i.is_deleted, i.row_version
                 FROM ideas i
                 WHERE {where}
                 ORDER BY {orderCol} {orderDir};
                 """;

            if (searchLike is not null)
            {
                AddParam(cmd, "$like", searchLike);
            }

            if (query.StatusFilter is { Length: > 0 } st)
            {
                AddParam(cmd, "$st", st);
            }

            if (query.PriorityFilter is { Length: > 0 } pr)
            {
                AddParam(cmd, "$pri", pr);
            }

            var list = new List<Idea>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(ReadIdea(reader));
            }

            return (IReadOnlyList<Idea>)list;
        }, cancellationToken);
    }

    public Task<Idea?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    id, title, description, tech_stack, status, priority, linked_project_id,
                    created_at, updated_at, is_deleted, row_version
                FROM ideas
                WHERE id = $id AND is_deleted = 0;
                """;
            AddParam(cmd, "$id", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return ReadIdea(reader);
        }, cancellationToken);

    public Task InsertAsync(Idea idea, CancellationToken cancellationToken = default)
    {
        IdeaFieldValidator.ValidateForInsert(idea);
        var linked = IdeaFieldValidator.NormalizeLinkedProjectId(idea.Status, idea.LinkedProjectId);
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO ideas (
                    id, title, description, tech_stack, status, priority, linked_project_id,
                    created_at, updated_at, is_deleted, row_version)
                VALUES (
                    $id, $title, $desc, $tstack, $status, $pri, $lpid,
                    $ca, $ua, 0, 1);
                """;
            BindIdeaWrite(cmd, idea, linked);
            try
            {
                _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067 || ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("该灵感标题已存在，请修改。", ex);
            }

            return 0;
        }, cancellationToken);
    }

    public Task UpdateAsync(Idea idea, CancellationToken cancellationToken = default)
    {
        _ = IdeaFieldValidator.ValidateTitle(idea.Title);
        _ = IdeaFieldValidator.ValidateDescription(idea.Description);
        _ = IdeaFieldValidator.ValidateTechStack(idea.TechStack);
        _ = IdeaFieldValidator.ValidateStatus(idea.Status);
        _ = IdeaFieldValidator.ValidatePriority(idea.Priority);
        IdeaFieldValidator.ValidateLinkedProject(idea.Status, idea.LinkedProjectId);
        var linked = IdeaFieldValidator.NormalizeLinkedProjectId(idea.Status, idea.LinkedProjectId);

        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE ideas
                SET title = $title,
                    description = $desc,
                    tech_stack = $tstack,
                    status = $status,
                    priority = $pri,
                    linked_project_id = $lpid,
                    updated_at = $ua,
                    row_version = row_version + 1
                WHERE id = $id AND is_deleted = 0 AND row_version = $rv;
                """;
            AddParam(cmd, "$id", idea.Id);
            AddParam(cmd, "$title", idea.Title);
            AddParam(cmd, "$desc", idea.Description);
            AddParam(cmd, "$tstack", idea.TechStack);
            AddParam(cmd, "$status", idea.Status);
            AddParam(cmd, "$pri", idea.Priority ?? (object)DBNull.Value);
            AddParam(cmd, "$lpid", linked ?? (object)DBNull.Value);
            AddParam(cmd, "$ua", idea.UpdatedAt);
            AddParam(cmd, "$rv", idea.RowVersion);
            try
            {
                var n = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new InvalidOperationException("保存失败：灵感已被修改或不存在。");
                }
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067 || ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("该灵感标题已存在，请修改。", ex);
            }

            return 0;
        }, cancellationToken);
    }

    public Task SoftDeleteAsync(string id, long expectedRowVersion, CancellationToken cancellationToken = default) =>
        holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var tx = await db.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var now = NowStamp();
                await using (var rel = db.CreateCommand())
                {
                    rel.Transaction = tx;
                    rel.CommandText =
                        """
                        UPDATE idea_documents
                        SET is_deleted = 1,
                            row_version = row_version + 1
                        WHERE idea_id = $id AND is_deleted = 0;
                        """;
                    AddParam(rel, "$id", id);
                    _ = await rel.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                await using (var cmd = db.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        """
                        UPDATE ideas
                        SET is_deleted = 1,
                            updated_at = $ua,
                            row_version = row_version + 1
                        WHERE id = $id AND is_deleted = 0 AND row_version = $rv;
                        """;
                    AddParam(cmd, "$id", id);
                    AddParam(cmd, "$ua", now);
                    AddParam(cmd, "$rv", expectedRowVersion);
                    var n = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    if (n == 0)
                    {
                        throw new InvalidOperationException("删除失败：灵感已被修改或不存在。");
                    }
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

    public Task<IReadOnlyList<IdeaDocumentLink>> ListDocumentLinksAsync(string ideaId, CancellationToken cancellationToken = default) =>
        holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    id.id, id.idea_id, id.document_id,
                    COALESCE(d.name, ''),
                    id.created_at, id.is_deleted, id.row_version
                FROM idea_documents id
                LEFT JOIN documents d ON d.id = id.document_id AND d.is_deleted = 0
                WHERE id.idea_id = $iid AND id.is_deleted = 0
                ORDER BY id.created_at DESC, id.id;
                """;
            AddParam(cmd, "$iid", ideaId);
            var list = new List<IdeaDocumentLink>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(ReadLink(reader));
            }

            return (IReadOnlyList<IdeaDocumentLink>)list;
        }, cancellationToken);

    public async Task AddDocumentLinkAsync(string ideaId, string documentId, CancellationToken cancellationToken = default)
    {
        if (await GetByIdAsync(ideaId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new InvalidOperationException("灵感不存在或已删除。");
        }

        if (await documentRepository.GetByIdAsync(documentId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new InvalidOperationException("文档不存在或已删除。");
        }

        var now = NowStamp();
        var linkId = Guid.NewGuid().ToString("D");
        await holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO idea_documents (id, idea_id, document_id, created_at, is_deleted, row_version)
                VALUES ($id, $iid, $did, $ca, 0, 1);
                """;
            AddParam(cmd, "$id", linkId);
            AddParam(cmd, "$iid", ideaId);
            AddParam(cmd, "$did", documentId);
            AddParam(cmd, "$ca", now);
            try
            {
                _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067 || ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("该文档已与当前灵感关联。", ex);
            }

            return 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task RemoveDocumentLinkAsync(string linkId, long expectedRowVersion, CancellationToken cancellationToken = default) =>
        holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE idea_documents
                SET is_deleted = 1,
                    row_version = row_version + 1
                WHERE id = $id AND is_deleted = 0 AND row_version = $rv;
                """;
            AddParam(cmd, "$id", linkId);
            AddParam(cmd, "$rv", expectedRowVersion);
            var n = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (n == 0)
            {
                throw new InvalidOperationException("移除关联失败：记录已被修改或不存在。");
            }

            return 0;
        }, cancellationToken);

    private static Idea ReadIdea(DbDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            Title = reader.GetString(1),
            Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            TechStack = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Status = reader.GetString(4),
            Priority = reader.IsDBNull(5) ? null : reader.GetString(5),
            LinkedProjectId = reader.IsDBNull(6) ? null : reader.GetString(6),
            CreatedAt = reader.GetString(7),
            UpdatedAt = reader.GetString(8),
            IsDeleted = reader.GetInt32(9) != 0,
            RowVersion = reader.GetInt64(10),
        };

    private static IdeaDocumentLink ReadLink(DbDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            IdeaId = reader.GetString(1),
            DocumentId = reader.GetString(2),
            DocumentName = reader.GetString(3),
            CreatedAt = reader.GetString(4),
            IsDeleted = reader.GetInt32(5) != 0,
            RowVersion = reader.GetInt64(6),
        };

    private static void BindIdeaWrite(DbCommand cmd, Idea idea, string? linkedProjectId)
    {
        AddParam(cmd, "$id", idea.Id);
        AddParam(cmd, "$title", idea.Title);
        AddParam(cmd, "$desc", idea.Description);
        AddParam(cmd, "$tstack", idea.TechStack);
        AddParam(cmd, "$status", idea.Status);
        AddParam(cmd, "$pri", idea.Priority ?? (object)DBNull.Value);
        AddParam(cmd, "$lpid", linkedProjectId ?? (object)DBNull.Value);
        AddParam(cmd, "$ca", idea.CreatedAt);
        AddParam(cmd, "$ua", idea.UpdatedAt);
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

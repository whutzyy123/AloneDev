using System.Data.Common;
using System.Globalization;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;
using PMTool.Core.Validation;

namespace PMTool.Infrastructure.Data;

public sealed class DocumentRepository(
    ISqliteConnectionHolder holder,
    ICurrentAccountContext accountContext) : IDocumentRepository
{
    public Task<IReadOnlyList<PmDocument>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    id, project_id, feature_id, name, relate_type, content, content_format, is_code_snippet,
                    created_at, updated_at, is_deleted, row_version
                FROM documents
                WHERE is_deleted = 0
                ORDER BY
                    CASE relate_type
                        WHEN '全局文档' THEN 0
                        WHEN '项目' THEN 1
                        WHEN '特性' THEN 2
                        ELSE 3
                    END,
                    updated_at DESC;
                """;
            var list = new List<PmDocument>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(ReadDocument(reader));
            }

            return (IReadOnlyList<PmDocument>)list;
        }, cancellationToken);
    }

    public Task<PmDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    id, project_id, feature_id, name, relate_type, content, content_format, is_code_snippet,
                    created_at, updated_at, is_deleted, row_version
                FROM documents
                WHERE id = $id AND is_deleted = 0;
                """;
            AddParam(cmd, "$id", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return ReadDocument(reader);
        }, cancellationToken);

    public Task InsertAsync(PmDocument document, CancellationToken cancellationToken = default)
    {
        DocumentFieldValidator.ValidateForInsert(document);
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO documents (
                    id, project_id, feature_id, name, relate_type, content, content_format, is_code_snippet,
                    created_at, updated_at, is_deleted, row_version)
                VALUES (
                    $id, $pid, $fid, $name, $rtype, $content, $cformat, $snippet,
                    $ca, $ua, 0, 1);
                """;
            BindInsert(cmd, document);
            _ = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    public Task UpdateContentAsync(
        string id,
        string content,
        string contentFormat,
        long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        var c = DocumentFieldValidator.ValidateContent(content);
        var cf = DocumentFieldValidator.ValidateContentFormat(contentFormat);
        var now = NowStamp();
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE documents
                SET content = $content,
                    content_format = $cformat,
                    updated_at = $ua,
                    row_version = row_version + 1
                WHERE id = $id AND is_deleted = 0 AND row_version = $rv;
                """;
            AddParam(cmd, "$id", id);
            AddParam(cmd, "$content", c);
            AddParam(cmd, "$cformat", cf);
            AddParam(cmd, "$ua", now);
            AddParam(cmd, "$rv", expectedRowVersion);
            var n = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (n == 0)
            {
                throw new InvalidOperationException("保存失败：文档已被修改或不存在，请刷新后重试。");
            }

            return 0;
        }, cancellationToken);
    }

    public Task UpdateMetadataAsync(
        string id,
        string name,
        bool isCodeSnippet,
        long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        var n = DocumentFieldValidator.ValidateName(name);
        var now = NowStamp();
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE documents
                SET name = $name,
                    is_code_snippet = $snippet,
                    updated_at = $ua,
                    row_version = row_version + 1
                WHERE id = $id AND is_deleted = 0 AND row_version = $rv;
                """;
            AddParam(cmd, "$id", id);
            AddParam(cmd, "$name", n);
            AddParam(cmd, "$snippet", isCodeSnippet ? 1 : 0);
            AddParam(cmd, "$ua", now);
            AddParam(cmd, "$rv", expectedRowVersion);
            var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (rows == 0)
            {
                throw new InvalidOperationException("更新失败：文档已被修改或不存在。");
            }

            return 0;
        }, cancellationToken);
    }

    public Task UpdateFullAsync(
        string id,
        string name,
        bool isCodeSnippet,
        string content,
        string contentFormat,
        long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        var n = DocumentFieldValidator.ValidateName(name);
        var c = DocumentFieldValidator.ValidateContent(content);
        var cf = DocumentFieldValidator.ValidateContentFormat(contentFormat);
        var now = NowStamp();
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE documents
                SET name = $name,
                    is_code_snippet = $snippet,
                    content = $content,
                    content_format = $cformat,
                    updated_at = $ua,
                    row_version = row_version + 1
                WHERE id = $id AND is_deleted = 0 AND row_version = $rv;
                """;
            AddParam(cmd, "$id", id);
            AddParam(cmd, "$name", n);
            AddParam(cmd, "$snippet", isCodeSnippet ? 1 : 0);
            AddParam(cmd, "$content", c);
            AddParam(cmd, "$cformat", cf);
            AddParam(cmd, "$ua", now);
            AddParam(cmd, "$rv", expectedRowVersion);
            var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (rows == 0)
            {
                throw new InvalidOperationException("保存失败：文档已被修改或不存在，请刷新后重试。");
            }

            return 0;
        }, cancellationToken);
    }

    public Task SoftDeleteAsync(string id, long expectedRowVersion, CancellationToken cancellationToken = default) =>
        holder.UseConnectionAsync(async (db, ct) =>
        {
            var now = NowStamp();
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                UPDATE documents
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
                throw new InvalidOperationException("删除失败：文档已被修改或不存在。");
            }

            TryDeleteImageFilesForDocument(id);
            return 0;
        }, cancellationToken);

    private void TryDeleteImageFilesForDocument(string documentId)
    {
        try
        {
            var root = accountContext.GetAccountDirectoryPath();
            var dir = Path.Combine(root, "Images");
            if (!Directory.Exists(dir))
            {
                return;
            }

            var pattern = $"{documentId}_*";
            foreach (var path in Directory.EnumerateFiles(dir, pattern))
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    // 忽略单文件删除失败
                }
            }
        }
        catch
        {
            // 忽略清理失败
        }
    }

    private static PmDocument ReadDocument(DbDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            ProjectId = reader.IsDBNull(1) ? null : reader.GetString(1),
            FeatureId = reader.IsDBNull(2) ? null : reader.GetString(2),
            Name = reader.GetString(3),
            RelateType = reader.GetString(4),
            Content = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            ContentFormat = reader.GetString(6),
            IsCodeSnippet = reader.GetInt32(7) != 0,
            CreatedAt = reader.GetString(8),
            UpdatedAt = reader.GetString(9),
            IsDeleted = reader.GetInt32(10) != 0,
            RowVersion = reader.GetInt64(11),
        };

    private static void BindInsert(DbCommand cmd, PmDocument document)
    {
        AddParam(cmd, "$id", document.Id);
        AddParam(cmd, "$pid", document.ProjectId ?? (object)DBNull.Value);
        AddParam(cmd, "$fid", document.FeatureId ?? (object)DBNull.Value);
        AddParam(cmd, "$name", document.Name);
        AddParam(cmd, "$rtype", document.RelateType);
        AddParam(cmd, "$content", document.Content);
        AddParam(cmd, "$cformat", document.ContentFormat);
        AddParam(cmd, "$snippet", document.IsCodeSnippet ? 1 : 0);
        AddParam(cmd, "$ca", document.CreatedAt);
        AddParam(cmd, "$ua", document.UpdatedAt);
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

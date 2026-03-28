using System.Data.Common;
using PMTool.Core.Validation;

namespace PMTool.Infrastructure.Data;

/// <summary>SQLite schema upgrades keyed by <c>PRAGMA user_version</c>.</summary>
internal static class SchemaMigration
{
    private const int TargetUserVersion = 11;

    internal static async Task ApplyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var current = await GetUserVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        if (current >= TargetUserVersion)
        {
            return;
        }

        if (current < 3)
        {
            await MigrateToV3Async(connection, cancellationToken).ConfigureAwait(false);
        }

        if (current < 4)
        {
            await MigrateToV4Async(connection, cancellationToken).ConfigureAwait(false);
        }

        if (current < 5)
        {
            await MigrateToV5Async(connection, cancellationToken).ConfigureAwait(false);
        }

        if (current < 6)
        {
            await MigrateToV6Async(connection, cancellationToken).ConfigureAwait(false);
        }

        if (current < 7)
        {
            await MigrateToV7Async(connection, cancellationToken).ConfigureAwait(false);
        }

        if (current < 8)
        {
            await MigrateToV8Async(connection, cancellationToken).ConfigureAwait(false);
        }

        if (current < 9)
        {
            await MigrateToV9Async(connection, cancellationToken).ConfigureAwait(false);
        }

        if (current < 10)
        {
            await MigrateToV10Async(connection, cancellationToken).ConfigureAwait(false);
        }

        if (current < 11)
        {
            await MigrateToV11Async(connection, cancellationToken).ConfigureAwait(false);
        }

        await SetUserVersionAsync(connection, TargetUserVersion, cancellationToken).ConfigureAwait(false);
    }

    private static async Task MigrateToV7Async(DbConnection connection, CancellationToken cancellationToken)
    {
        var ideaCols = await GetColumnNamesAsync(connection, "ideas", cancellationToken).ConfigureAwait(false);
        if (ideaCols.Contains("title"))
        {
            await EnsureIdeaDocumentsTableAsync(connection, cancellationToken).ConfigureAwait(false);
            await EnsureIdeaIndexesAsync(connection, cancellationToken).ConfigureAwait(false);
            return;
        }

        await ExecAsync(connection, cancellationToken, "BEGIN IMMEDIATE;").ConfigureAwait(false);
        try
        {
            await ExecAsync(connection, cancellationToken, """
                CREATE TABLE ideas_new (
                    id TEXT NOT NULL PRIMARY KEY,
                    title TEXT NOT NULL DEFAULT '',
                    description TEXT NOT NULL DEFAULT '',
                    tech_stack TEXT NOT NULL DEFAULT '',
                    status TEXT NOT NULL DEFAULT '待评估',
                    priority TEXT NULL,
                    linked_project_id TEXT NULL,
                    is_deleted INTEGER NOT NULL DEFAULT 0,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    row_version INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (linked_project_id) REFERENCES projects(id)
                );
                """).ConfigureAwait(false);

            await ExecAsync(connection, cancellationToken, """
                INSERT INTO ideas_new (
                    id, title, description, tech_stack, status, priority, linked_project_id,
                    is_deleted, created_at, updated_at, row_version)
                SELECT
                    id,
                    '灵感-' || substr(id, 1, 8),
                    '',
                    '',
                    '待评估',
                    NULL,
                    linked_project_id,
                    is_deleted,
                    created_at,
                    updated_at,
                    row_version
                FROM ideas;
                """).ConfigureAwait(false);

            await ExecAsync(connection, cancellationToken, "DROP TABLE ideas;").ConfigureAwait(false);
            await ExecAsync(connection, cancellationToken, "ALTER TABLE ideas_new RENAME TO ideas;").ConfigureAwait(false);

            await EnsureIdeaDocumentsTableAsync(connection, cancellationToken).ConfigureAwait(false);
            await EnsureIdeaIndexesAsync(connection, cancellationToken).ConfigureAwait(false);

            await ExecAsync(connection, cancellationToken, "COMMIT;").ConfigureAwait(false);
        }
        catch
        {
            await ExecAsync(connection, cancellationToken, "ROLLBACK;").ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>文档关联类型文案：历史库为「特性」，与应用常量「模块」对齐。</summary>
    private static async Task MigrateToV8Async(DbConnection connection, CancellationToken cancellationToken)
    {
        var docCols = await GetColumnNamesAsync(connection, "documents", cancellationToken).ConfigureAwait(false);
        if (!docCols.Contains("relate_type"))
        {
            return;
        }

        await ExecAsync(connection, cancellationToken,
            "UPDATE documents SET relate_type = '模块' WHERE relate_type = '特性';").ConfigureAwait(false);
    }

    private static async Task MigrateToV9Async(DbConnection connection, CancellationToken cancellationToken)
    {
        var cols = await GetColumnNamesAsync(connection, "projects", cancellationToken).ConfigureAwait(false);
        await AddColumnIfMissingAsync(
            connection,
            cancellationToken,
            cols,
            "local_git_root",
            "ALTER TABLE projects ADD COLUMN local_git_root TEXT NULL;").ConfigureAwait(false);
    }

    private static async Task MigrateToV10Async(DbConnection connection, CancellationToken cancellationToken)
    {
        var docCols = await GetColumnNamesAsync(connection, "documents", cancellationToken).ConfigureAwait(false);
        await AddColumnIfMissingAsync(
            connection,
            cancellationToken,
            docCols,
            "snippet_language",
            "ALTER TABLE documents ADD COLUMN snippet_language TEXT NULL;").ConfigureAwait(false);
    }

    private static async Task MigrateToV11Async(DbConnection connection, CancellationToken cancellationToken)
    {
        var projCols = await GetColumnNamesAsync(connection, "projects", cancellationToken).ConfigureAwait(false);
        await AddColumnIfMissingAsync(
            connection,
            cancellationToken,
            projCols,
            "tech_stack",
            "ALTER TABLE projects ADD COLUMN tech_stack TEXT NOT NULL DEFAULT '';").ConfigureAwait(false);
    }

    private static async Task EnsureIdeaDocumentsTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ExecAsync(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS idea_documents (
                id TEXT NOT NULL PRIMARY KEY,
                idea_id TEXT NOT NULL,
                document_id TEXT NOT NULL,
                created_at TEXT NOT NULL,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                row_version INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (idea_id) REFERENCES ideas(id),
                FOREIGN KEY (document_id) REFERENCES documents(id)
            );
            """).ConfigureAwait(false);

        await ExecAsync(connection, cancellationToken, """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_idea_documents_pair_active
            ON idea_documents (idea_id, document_id)
            WHERE is_deleted = 0;
            """).ConfigureAwait(false);

        await ExecAsync(connection, cancellationToken, """
            CREATE INDEX IF NOT EXISTS idx_idea_documents_idea
            ON idea_documents (idea_id);
            """).ConfigureAwait(false);

        await ExecAsync(connection, cancellationToken, """
            CREATE INDEX IF NOT EXISTS idx_idea_documents_document
            ON idea_documents (document_id);
            """).ConfigureAwait(false);
    }

    private static async Task EnsureIdeaIndexesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ExecAsync(connection, cancellationToken, """
            CREATE INDEX IF NOT EXISTS idx_ideas_list
            ON ideas (is_deleted, updated_at DESC);
            """).ConfigureAwait(false);

        await ExecAsync(connection, cancellationToken, """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_ideas_title_active
            ON ideas (title COLLATE NOCASE)
            WHERE is_deleted = 0;
            """).ConfigureAwait(false);
    }

    private static async Task MigrateToV6Async(DbConnection connection, CancellationToken cancellationToken)
    {
        var cols = await GetColumnNamesAsync(connection, "documents", cancellationToken).ConfigureAwait(false);
        if (cols.Contains("content_format"))
        {
            return;
        }

        await ExecAsync(connection, cancellationToken, "BEGIN IMMEDIATE;").ConfigureAwait(false);
        try
        {
            await ExecAsync(connection, cancellationToken, """
                CREATE TABLE documents_new (
                    id TEXT NOT NULL PRIMARY KEY,
                    project_id TEXT NULL,
                    feature_id TEXT NULL,
                    name TEXT NOT NULL DEFAULT '',
                    relate_type TEXT NOT NULL DEFAULT '项目',
                    content TEXT NOT NULL DEFAULT '',
                    content_format TEXT NOT NULL DEFAULT 'Markdown',
                    is_code_snippet INTEGER NOT NULL DEFAULT 0,
                    is_deleted INTEGER NOT NULL DEFAULT 0,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    row_version INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (project_id) REFERENCES projects(id),
                    FOREIGN KEY (feature_id) REFERENCES features(id)
                );
                """).ConfigureAwait(false);

            await ExecAsync(connection, cancellationToken, """
                INSERT INTO documents_new (
                    id, project_id, feature_id, name, relate_type, content, content_format, is_code_snippet,
                    is_deleted, created_at, updated_at, row_version)
                SELECT
                    id,
                    project_id,
                    NULL,
                    '文档-' || substr(id, 1, 8),
                    relate_type,
                    '',
                    'Markdown',
                    0,
                    is_deleted,
                    created_at,
                    updated_at,
                    row_version
                FROM documents;
                """).ConfigureAwait(false);

            await ExecAsync(connection, cancellationToken, "DROP TABLE documents;").ConfigureAwait(false);
            await ExecAsync(connection, cancellationToken, "ALTER TABLE documents_new RENAME TO documents;").ConfigureAwait(false);

            await ExecAsync(connection, cancellationToken, """
                CREATE INDEX IF NOT EXISTS idx_documents_list
                ON documents (is_deleted, project_id, feature_id, updated_at DESC);
                """).ConfigureAwait(false);

            await ExecAsync(connection, cancellationToken, "COMMIT;").ConfigureAwait(false);
        }
        catch
        {
            await ExecAsync(connection, cancellationToken, "ROLLBACK;").ConfigureAwait(false);
            throw;
        }
    }

    private static async Task MigrateToV5Async(DbConnection connection, CancellationToken cancellationToken)
    {
        var cols = await GetColumnNamesAsync(connection, "releases", cancellationToken).ConfigureAwait(false);

        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "name",
            "ALTER TABLE releases ADD COLUMN name TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "description",
            "ALTER TABLE releases ADD COLUMN description TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "start_at",
            "ALTER TABLE releases ADD COLUMN start_at TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "end_at",
            "ALTER TABLE releases ADD COLUMN end_at TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "status",
            "ALTER TABLE releases ADD COLUMN status TEXT NOT NULL DEFAULT '未开始';");

        await ExecAsync(connection, cancellationToken,
            "UPDATE releases SET name = '遗留-' || substr(id, 1, 8) WHERE trim(name) = '' OR name IS NULL;");
        await ExecAsync(connection, cancellationToken,
            "UPDATE releases SET start_at = created_at WHERE trim(start_at) = '' OR start_at IS NULL;");
        await ExecAsync(connection, cancellationToken,
            "UPDATE releases SET end_at = updated_at WHERE trim(end_at) = '' OR end_at IS NULL;");

        await ExecAsync(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS release_relations (
                id TEXT NOT NULL PRIMARY KEY,
                release_id TEXT NOT NULL,
                target_type TEXT NOT NULL,
                target_id TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (release_id) REFERENCES releases(id),
                UNIQUE (release_id, target_type, target_id)
            );
            """).ConfigureAwait(false);

        await ExecAsync(connection, cancellationToken, """
            CREATE INDEX IF NOT EXISTS idx_releases_list
            ON releases (project_id, is_deleted, updated_at DESC);
            """).ConfigureAwait(false);

        await ExecAsync(connection, cancellationToken, """
            CREATE INDEX IF NOT EXISTS idx_release_relations_release
            ON release_relations (release_id);
            """).ConfigureAwait(false);
    }

    private static async Task MigrateToV3Async(DbConnection connection, CancellationToken cancellationToken)
    {
        var cols = await GetColumnNamesAsync(connection, "features", cancellationToken).ConfigureAwait(false);

        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "name", "ALTER TABLE features ADD COLUMN name TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "description", "ALTER TABLE features ADD COLUMN description TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "status", "ALTER TABLE features ADD COLUMN status TEXT NOT NULL DEFAULT '待规划';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "priority", "ALTER TABLE features ADD COLUMN priority INTEGER NOT NULL DEFAULT 2;");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "acceptance_criteria", "ALTER TABLE features ADD COLUMN acceptance_criteria TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "tech_stack", "ALTER TABLE features ADD COLUMN tech_stack TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "notes", "ALTER TABLE features ADD COLUMN notes TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "due_date", "ALTER TABLE features ADD COLUMN due_date TEXT NULL;");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "attachments_placeholder", "ALTER TABLE features ADD COLUMN attachments_placeholder TEXT NULL;");

        await ExecAsync(connection, cancellationToken,
            "UPDATE features SET name = '遗留-' || id WHERE trim(name) = '' OR name IS NULL;").ConfigureAwait(false);

        cols = await GetColumnNamesAsync(connection, "tasks", cancellationToken).ConfigureAwait(false);
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "feature_id",
            "ALTER TABLE tasks ADD COLUMN feature_id TEXT NULL REFERENCES features(id);").ConfigureAwait(false);

        await ExecAsync(connection, cancellationToken, """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_features_project_name_active
            ON features (project_id, name COLLATE NOCASE)
            WHERE is_deleted = 0;
            """).ConfigureAwait(false);

        await ExecAsync(connection, cancellationToken, """
            CREATE INDEX IF NOT EXISTS idx_features_list
            ON features (is_deleted, project_id, updated_at DESC);
            """).ConfigureAwait(false);
    }

    private static async Task MigrateToV4Async(DbConnection connection, CancellationToken cancellationToken)
    {
        var cols = await GetColumnNamesAsync(connection, "tasks", cancellationToken).ConfigureAwait(false);

        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "name",
            "ALTER TABLE tasks ADD COLUMN name TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "description",
            "ALTER TABLE tasks ADD COLUMN description TEXT NOT NULL DEFAULT '';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "task_type",
            "ALTER TABLE tasks ADD COLUMN task_type TEXT NOT NULL DEFAULT 'Feature';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "status",
            "ALTER TABLE tasks ADD COLUMN status TEXT NOT NULL DEFAULT '未开始';");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "severity",
            "ALTER TABLE tasks ADD COLUMN severity TEXT NULL;");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "estimated_hours",
            "ALTER TABLE tasks ADD COLUMN estimated_hours REAL NOT NULL DEFAULT 0;");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "actual_hours",
            "ALTER TABLE tasks ADD COLUMN actual_hours REAL NOT NULL DEFAULT 0;");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "completed_at",
            "ALTER TABLE tasks ADD COLUMN completed_at TEXT NULL;");
        await AddColumnIfMissingAsync(connection, cancellationToken, cols, "sort_value",
            "ALTER TABLE tasks ADD COLUMN sort_value INTEGER NOT NULL DEFAULT 0;");

        await ExecAsync(connection, cancellationToken,
            "UPDATE tasks SET name = '遗留-' || id WHERE trim(name) = '' OR name IS NULL;").ConfigureAwait(false);

        await NormalizeTaskSortValuesAsync(connection, cancellationToken).ConfigureAwait(false);

        await ExecAsync(connection, cancellationToken, """
            CREATE INDEX IF NOT EXISTS idx_tasks_feature_order
            ON tasks (feature_id, is_deleted, sort_value);
            """).ConfigureAwait(false);
    }

    private static async Task NormalizeTaskSortValuesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        await using (var sel = connection.CreateCommand())
        {
            sel.CommandText =
                "SELECT id, feature_id FROM tasks WHERE is_deleted = 0 ORDER BY COALESCE(feature_id, ''), created_at, id;";
            await using var r = await sel.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = r.GetString(0);
                var fk = r.IsDBNull(1) ? string.Empty : r.GetString(1);
                if (!groups.TryGetValue(fk, out var list))
                {
                    list = [];
                    groups[fk] = list;
                }

                list.Add(id);
            }
        }

        foreach (var kv in groups)
        {
            for (var i = 0; i < kv.Value.Count; i++)
            {
                await using var up = connection.CreateCommand();
                up.CommandText = "UPDATE tasks SET sort_value = $sv WHERE id = $id;";
                AddParameter(up, "$sv", i);
                AddParameter(up, "$id", kv.Value[i]);
                _ = await up.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void AddParameter(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static async Task AddColumnIfMissingAsync(
        DbConnection connection,
        CancellationToken cancellationToken,
        HashSet<string> knownCols,
        string columnName,
        string alterSql)
    {
        if (knownCols.Contains(columnName))
        {
            return;
        }

        await ExecAsync(connection, cancellationToken, alterSql).ConfigureAwait(false);
        knownCols.Add(columnName);
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        SqliteIdentifierValidator.ThrowIfInvalidTableName(table);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = r.GetString(1);
            _ = set.Add(name);
        }

        return set;
    }

    private static async Task<int> GetUserVersionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var o = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return o is long l ? (int)l : Convert.ToInt32(o);
    }

    private static async Task SetUserVersionAsync(DbConnection connection, int version, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        _ = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecAsync(DbConnection connection, CancellationToken cancellationToken, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        _ = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

using System.Data.Common;
using PMTool.Core.Validation;

namespace PMTool.Infrastructure.Data;

internal static class ProjectsSchema
{
    internal static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await Exec(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS projects (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                description TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL,
                category TEXT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                row_version INTEGER NOT NULL DEFAULT 1
            );
            """).ConfigureAwait(false);

        await Exec(connection, cancellationToken, """
            CREATE INDEX IF NOT EXISTS idx_projects_list
            ON projects (is_deleted, updated_at DESC);
            """).ConfigureAwait(false);

        await Exec(connection, cancellationToken, """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_projects_name_status_active
            ON projects (name COLLATE NOCASE, status)
            WHERE is_deleted = 0;
            """).ConfigureAwait(false);

        await Exec(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS features (
                id TEXT NOT NULL PRIMARY KEY,
                project_id TEXT NOT NULL,
                name TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT '待规划',
                priority INTEGER NOT NULL DEFAULT 2,
                acceptance_criteria TEXT NOT NULL DEFAULT '',
                tech_stack TEXT NOT NULL DEFAULT '',
                notes TEXT NOT NULL DEFAULT '',
                due_date TEXT NULL,
                attachments_placeholder TEXT NULL,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                row_version INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (project_id) REFERENCES projects(id)
            );
            """).ConfigureAwait(false);

        await Exec(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS tasks (
                id TEXT NOT NULL PRIMARY KEY,
                project_id TEXT NOT NULL,
                feature_id TEXT NULL,
                name TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                task_type TEXT NOT NULL DEFAULT 'Feature',
                status TEXT NOT NULL DEFAULT '未开始',
                severity TEXT NULL,
                estimated_hours REAL NOT NULL DEFAULT 0,
                actual_hours REAL NOT NULL DEFAULT 0,
                completed_at TEXT NULL,
                sort_value INTEGER NOT NULL DEFAULT 0,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                row_version INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (project_id) REFERENCES projects(id),
                FOREIGN KEY (feature_id) REFERENCES features(id)
            );
            """).ConfigureAwait(false);

        var taskCols = await GetColumnNamesAsync(connection, "tasks", cancellationToken).ConfigureAwait(false);
        if (taskCols.Contains("feature_id"))
        {
            if (taskCols.Contains("sort_value"))
            {
                await Exec(connection, cancellationToken, """
                    CREATE INDEX IF NOT EXISTS idx_tasks_feature_order
                    ON tasks (feature_id, is_deleted, sort_value);
                    """).ConfigureAwait(false);
            }
            else if (taskCols.Contains("updated_at"))
            {
                await Exec(connection, cancellationToken, """
                    CREATE INDEX IF NOT EXISTS idx_tasks_feature_order
                    ON tasks (feature_id, is_deleted, updated_at DESC);
                    """).ConfigureAwait(false);
            }
            else
            {
                await Exec(connection, cancellationToken, """
                    CREATE INDEX IF NOT EXISTS idx_tasks_feature_order
                    ON tasks (feature_id, is_deleted);
                    """).ConfigureAwait(false);
            }
        }
        else
        {
            // 兼容旧库：feature_id 尚未迁移前使用 project 维度索引兜底，避免启动阶段崩溃。
            if (taskCols.Contains("project_id") && taskCols.Contains("sort_value"))
            {
                await Exec(connection, cancellationToken, """
                    CREATE INDEX IF NOT EXISTS idx_tasks_project_order
                    ON tasks (project_id, is_deleted, sort_value);
                    """).ConfigureAwait(false);
            }
            else if (taskCols.Contains("project_id") && taskCols.Contains("updated_at"))
            {
                await Exec(connection, cancellationToken, """
                    CREATE INDEX IF NOT EXISTS idx_tasks_project_order
                    ON tasks (project_id, is_deleted, updated_at DESC);
                    """).ConfigureAwait(false);
            }
            else if (taskCols.Contains("project_id"))
            {
                await Exec(connection, cancellationToken, """
                    CREATE INDEX IF NOT EXISTS idx_tasks_project_order
                    ON tasks (project_id, is_deleted);
                    """).ConfigureAwait(false);
            }
            else
            {
                await Exec(connection, cancellationToken, """
                    CREATE INDEX IF NOT EXISTS idx_tasks_active
                    ON tasks (is_deleted);
                    """).ConfigureAwait(false);
            }
        }

        await Exec(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS releases (
                id TEXT NOT NULL PRIMARY KEY,
                project_id TEXT NOT NULL,
                name TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                start_at TEXT NOT NULL DEFAULT '',
                end_at TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT '未开始',
                is_deleted INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                row_version INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (project_id) REFERENCES projects(id)
            );
            """).ConfigureAwait(false);

        await Exec(connection, cancellationToken, """
            CREATE INDEX IF NOT EXISTS idx_releases_list
            ON releases (project_id, is_deleted, updated_at DESC);
            """).ConfigureAwait(false);

        await Exec(connection, cancellationToken, """
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

        await Exec(connection, cancellationToken, """
            CREATE INDEX IF NOT EXISTS idx_release_relations_release
            ON release_relations (release_id);
            """).ConfigureAwait(false);

        await Exec(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS documents (
                id TEXT NOT NULL PRIMARY KEY,
                project_id TEXT NULL,
                feature_id TEXT NULL,
                name TEXT NOT NULL DEFAULT '',
                relate_type TEXT NOT NULL DEFAULT '项目',
                content TEXT NOT NULL DEFAULT '',
                content_format TEXT NOT NULL DEFAULT 'Markdown',
                is_code_snippet INTEGER NOT NULL DEFAULT 0,
                snippet_language TEXT NULL,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                row_version INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (project_id) REFERENCES projects(id),
                FOREIGN KEY (feature_id) REFERENCES features(id)
            );
            """).ConfigureAwait(false);

        var documentCols = await GetColumnNamesAsync(connection, "documents", cancellationToken).ConfigureAwait(false);
        if (documentCols.Contains("feature_id"))
        {
            await Exec(connection, cancellationToken, """
                CREATE INDEX IF NOT EXISTS idx_documents_list
                ON documents (is_deleted, project_id, feature_id, updated_at DESC);
                """).ConfigureAwait(false);
        }
        else
        {
            await Exec(connection, cancellationToken, """
                CREATE INDEX IF NOT EXISTS idx_documents_list
                ON documents (is_deleted, project_id, updated_at DESC);
                """).ConfigureAwait(false);
        }

        await Exec(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS ideas (
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

        var ideaCols = await GetColumnNamesAsync(connection, "ideas", cancellationToken).ConfigureAwait(false);
        if (ideaCols.Contains("is_deleted") && ideaCols.Contains("updated_at"))
        {
            await Exec(connection, cancellationToken, """
                CREATE INDEX IF NOT EXISTS idx_ideas_list
                ON ideas (is_deleted, updated_at DESC);
                """).ConfigureAwait(false);
        }
        else if (ideaCols.Contains("updated_at"))
        {
            await Exec(connection, cancellationToken, """
                CREATE INDEX IF NOT EXISTS idx_ideas_list
                ON ideas (updated_at DESC);
                """).ConfigureAwait(false);
        }
        else if (ideaCols.Contains("is_deleted"))
        {
            await Exec(connection, cancellationToken, """
                CREATE INDEX IF NOT EXISTS idx_ideas_list
                ON ideas (is_deleted);
                """).ConfigureAwait(false);
        }

        if (ideaCols.Contains("title") && ideaCols.Contains("is_deleted"))
        {
            await Exec(connection, cancellationToken, """
                CREATE UNIQUE INDEX IF NOT EXISTS idx_ideas_title_active
                ON ideas (title COLLATE NOCASE)
                WHERE is_deleted = 0;
                """).ConfigureAwait(false);
        }

        await Exec(connection, cancellationToken, """
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

        var ideaDocCols = await GetColumnNamesAsync(connection, "idea_documents", cancellationToken).ConfigureAwait(false);
        if (ideaDocCols.Contains("idea_id") && ideaDocCols.Contains("document_id") && ideaDocCols.Contains("is_deleted"))
        {
            await Exec(connection, cancellationToken, """
                CREATE UNIQUE INDEX IF NOT EXISTS idx_idea_documents_pair_active
                ON idea_documents (idea_id, document_id)
                WHERE is_deleted = 0;
                """).ConfigureAwait(false);
        }

        if (ideaDocCols.Contains("idea_id"))
        {
            await Exec(connection, cancellationToken, """
                CREATE INDEX IF NOT EXISTS idx_idea_documents_idea
                ON idea_documents (idea_id);
                """).ConfigureAwait(false);
        }

        if (ideaDocCols.Contains("document_id"))
        {
            await Exec(connection, cancellationToken, """
                CREATE INDEX IF NOT EXISTS idx_idea_documents_document
                ON idea_documents (document_id);
                """).ConfigureAwait(false);
        }
    }

    private static async Task Exec(DbConnection connection, CancellationToken cancellationToken, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        _ = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        SqliteIdentifierValidator.ThrowIfInvalidTableName(tableName);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.IsDBNull(1))
            {
                _ = columns.Add(reader.GetString(1));
            }
        }

        return columns;
    }
}

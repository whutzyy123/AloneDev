using System.Data.Common;
using System.Diagnostics;
using PMTool.Core.Abstractions;
using PMTool.Core.Models.Search;

namespace PMTool.Infrastructure.Data;

public sealed class GlobalSearchRepository(ISqliteConnectionHolder holder) : IGlobalSearchRepository
{
    /// <summary>单模块最大行数，控制 LIKЕ 扫描成本。</summary>
    private const int DefaultPerModuleLimit = 120;

    /// <summary>文档正文参与匹配的前缀长度（PRD 性能 / 大文档）。</summary>
    private const int DocumentContentScanChars = 80_000;

    public Task<GlobalSearchResponse> SearchAsync(GlobalSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.NormalizedNeedle))
        {
            return Task.FromResult(new GlobalSearchResponse(Array.Empty<GlobalSearchHit>(), 0, false));
        }

        var limit = request.PerModuleLimit > 0 ? request.PerModuleLimit : DefaultPerModuleLimit;
        var needle = request.NormalizedNeedle;
        var like = "%" + EscapeLike(needle) + "%";
        var scope = request.Scope == GlobalSearchScope.None ? GlobalSearchScope.All : request.Scope;

        return holder.UseConnectionAsync(async (db, ct) =>
        {
            var sw = Stopwatch.StartNew();
            var hits = new List<GlobalSearchHit>();

            if (scope.HasFlag(GlobalSearchScope.Projects))
            {
                ct.ThrowIfCancellationRequested();
                await AddProjectHitsAsync(db, needle, like, limit, hits, ct).ConfigureAwait(false);
            }

            if (scope.HasFlag(GlobalSearchScope.Features))
            {
                ct.ThrowIfCancellationRequested();
                await AddFeatureHitsAsync(db, needle, like, limit, hits, ct).ConfigureAwait(false);
            }

            if (scope.HasFlag(GlobalSearchScope.Tasks))
            {
                ct.ThrowIfCancellationRequested();
                await AddTaskHitsAsync(db, needle, like, limit, hits, ct).ConfigureAwait(false);
            }

            if (scope.HasFlag(GlobalSearchScope.Documents))
            {
                ct.ThrowIfCancellationRequested();
                await AddDocumentHitsAsync(db, needle, like, limit, hits, ct).ConfigureAwait(false);
            }

            if (scope.HasFlag(GlobalSearchScope.Ideas))
            {
                ct.ThrowIfCancellationRequested();
                await AddIdeaHitsAsync(db, needle, like, limit, hits, ct).ConfigureAwait(false);
            }

            hits.Sort(CompareHits);
            sw.Stop();
            var elapsed = (int)sw.ElapsedMilliseconds;
            var hint = elapsed > 1000 || hits.Count > 500;
            return new GlobalSearchResponse(hits, elapsed, hint);
        }, cancellationToken);
    }

    private static int CompareHits(GlobalSearchHit a, GlobalSearchHit b)
    {
        var byScore = b.MatchScore.CompareTo(a.MatchScore);
        if (byScore != 0)
        {
            return byScore;
        }

        return string.Compare(b.UpdatedAt, a.UpdatedAt, StringComparison.Ordinal);
    }

    private static async Task AddProjectHitsAsync(
        DbConnection db,
        string needle,
        string like,
        int limit,
        List<GlobalSearchHit> hits,
        CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"""
             SELECT id, name, description, updated_at,
               (
                 (LENGTH(LOWER(name)) - LENGTH(REPLACE(LOWER(name), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
                 + (LENGTH(LOWER(description)) - LENGTH(REPLACE(LOWER(description), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
               ) AS match_score
             FROM projects
             WHERE is_deleted = 0
               AND (
                 LOWER(name) LIKE LOWER($like) ESCAPE '\'
                 OR LOWER(description) LIKE LOWER($like) ESCAPE '\'
               )
             ORDER BY match_score DESC, updated_at DESC
             LIMIT {limit};
             """;
        AddParam(cmd, "$needle", needle);
        AddParam(cmd, "$like", like);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var id = reader.GetString(0);
            var name = reader.GetString(1);
            var desc = reader.GetString(2);
            var updated = reader.GetString(3);
            var score = reader.GetInt32(4);
            hits.Add(new GlobalSearchHit
            {
                Module = GlobalSearchModule.Project,
                EntityId = id,
                Title = name,
                Snippet = BuildSnippet(name, desc, needle),
                MatchScore = score,
                UpdatedAt = updated,
                Jump = new GlobalSearchJumpContext { ProjectId = id },
            });
        }
    }

    private static async Task AddFeatureHitsAsync(
        DbConnection db,
        string needle,
        string like,
        int limit,
        List<GlobalSearchHit> hits,
        CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"""
             SELECT f.id, f.project_id, f.name, f.description, f.acceptance_criteria, f.tech_stack, f.updated_at,
               (
                 (LENGTH(LOWER(f.name)) - LENGTH(REPLACE(LOWER(f.name), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
                 + (LENGTH(LOWER(f.description)) - LENGTH(REPLACE(LOWER(f.description), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
                 + (LENGTH(LOWER(f.acceptance_criteria)) - LENGTH(REPLACE(LOWER(f.acceptance_criteria), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
                 + (LENGTH(LOWER(f.tech_stack)) - LENGTH(REPLACE(LOWER(f.tech_stack), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
               ) AS match_score
             FROM features f
             WHERE f.is_deleted = 0
               AND (
                 LOWER(f.name) LIKE LOWER($like) ESCAPE '\'
                 OR LOWER(f.description) LIKE LOWER($like) ESCAPE '\'
                 OR LOWER(f.acceptance_criteria) LIKE LOWER($like) ESCAPE '\'
                 OR LOWER(f.tech_stack) LIKE LOWER($like) ESCAPE '\'
               )
             ORDER BY match_score DESC, f.updated_at DESC
             LIMIT {limit};
             """;
        AddParam(cmd, "$needle", needle);
        AddParam(cmd, "$like", like);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var id = reader.GetString(0);
            var pid = reader.GetString(1);
            var name = reader.GetString(2);
            var desc = reader.GetString(3);
            var ac = reader.GetString(4);
            var ts = reader.GetString(5);
            var updated = reader.GetString(6);
            var score = reader.GetInt32(7);
            var blob = name + " · " + Truncate(desc, 60) + " · " + Truncate(ac, 60) + " · " + ts;
            hits.Add(new GlobalSearchHit
            {
                Module = GlobalSearchModule.Feature,
                EntityId = id,
                Title = name,
                Snippet = BuildSnippet(name, blob, needle),
                MatchScore = score,
                UpdatedAt = updated,
                Jump = new GlobalSearchJumpContext { ProjectId = pid, FeatureId = id },
            });
        }
    }

    private static async Task AddTaskHitsAsync(
        DbConnection db,
        string needle,
        string like,
        int limit,
        List<GlobalSearchHit> hits,
        CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"""
             SELECT t.id, t.project_id, t.feature_id, t.name, t.description, t.updated_at,
               (
                 (LENGTH(LOWER(t.name)) - LENGTH(REPLACE(LOWER(t.name), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
                 + (LENGTH(LOWER(t.description)) - LENGTH(REPLACE(LOWER(t.description), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
               ) AS match_score
             FROM tasks t
             WHERE t.is_deleted = 0
               AND (
                 LOWER(t.name) LIKE LOWER($like) ESCAPE '\'
                 OR LOWER(t.description) LIKE LOWER($like) ESCAPE '\'
               )
             ORDER BY match_score DESC, t.updated_at DESC
             LIMIT {limit};
             """;
        AddParam(cmd, "$needle", needle);
        AddParam(cmd, "$like", like);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var id = reader.GetString(0);
            var pid = reader.GetString(1);
            var fid = reader.IsDBNull(2) ? null : reader.GetString(2);
            var name = reader.GetString(3);
            var desc = reader.GetString(4);
            var updated = reader.GetString(5);
            var score = reader.GetInt32(6);
            hits.Add(new GlobalSearchHit
            {
                Module = GlobalSearchModule.Task,
                EntityId = id,
                Title = name,
                Snippet = BuildSnippet(name, desc, needle),
                MatchScore = score,
                UpdatedAt = updated,
                Jump = new GlobalSearchJumpContext { ProjectId = pid, FeatureId = fid },
            });
        }
    }

    private static async Task AddDocumentHitsAsync(
        DbConnection db,
        string needle,
        string like,
        int limit,
        List<GlobalSearchHit> hits,
        CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"""
             SELECT d.id, d.project_id, d.feature_id, d.name, d.updated_at,
               SUBSTR(d.content, 1, {DocumentContentScanChars}) AS scan_text,
               (
                 (LENGTH(LOWER(d.name)) - LENGTH(REPLACE(LOWER(d.name), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
                 + (LENGTH(LOWER(SUBSTR(d.content, 1, {DocumentContentScanChars}))) - LENGTH(REPLACE(LOWER(SUBSTR(d.content, 1, {DocumentContentScanChars})), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
               ) AS match_score
             FROM documents d
             WHERE d.is_deleted = 0
               AND (
                 LOWER(d.name) LIKE LOWER($like) ESCAPE '\'
                 OR LOWER(SUBSTR(d.content, 1, {DocumentContentScanChars})) LIKE LOWER($like) ESCAPE '\'
               )
             ORDER BY match_score DESC, d.updated_at DESC
             LIMIT {limit};
             """;
        AddParam(cmd, "$needle", needle);
        AddParam(cmd, "$like", like);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var id = reader.GetString(0);
            var pid = reader.IsDBNull(1) ? null : reader.GetString(1);
            var fid = reader.IsDBNull(2) ? null : reader.GetString(2);
            var name = reader.GetString(3);
            var updated = reader.GetString(4);
            var scanText = reader.GetString(5);
            var score = reader.GetInt32(6);
            hits.Add(new GlobalSearchHit
            {
                Module = GlobalSearchModule.Document,
                EntityId = id,
                Title = name,
                Snippet = BuildSnippet(name, scanText, needle),
                MatchScore = score,
                UpdatedAt = updated,
                Jump = new GlobalSearchJumpContext { ProjectId = pid, FeatureId = fid },
            });
        }
    }

    private static async Task AddIdeaHitsAsync(
        DbConnection db,
        string needle,
        string like,
        int limit,
        List<GlobalSearchHit> hits,
        CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"""
             SELECT i.id, i.title, i.description, i.tech_stack, i.updated_at,
               (
                 (LENGTH(LOWER(i.title)) - LENGTH(REPLACE(LOWER(i.title), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
                 + (LENGTH(LOWER(i.description)) - LENGTH(REPLACE(LOWER(i.description), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
                 + (LENGTH(LOWER(i.tech_stack)) - LENGTH(REPLACE(LOWER(i.tech_stack), LOWER($needle), ''))) / MAX(LENGTH($needle), 1)
               ) AS match_score
             FROM ideas i
             WHERE i.is_deleted = 0
               AND (
                 LOWER(i.title) LIKE LOWER($like) ESCAPE '\'
                 OR LOWER(i.description) LIKE LOWER($like) ESCAPE '\'
                 OR LOWER(i.tech_stack) LIKE LOWER($like) ESCAPE '\'
               )
             ORDER BY match_score DESC, i.updated_at DESC
             LIMIT {limit};
             """;
        AddParam(cmd, "$needle", needle);
        AddParam(cmd, "$like", like);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var id = reader.GetString(0);
            var title = reader.GetString(1);
            var desc = reader.GetString(2);
            var ts = reader.GetString(3);
            var updated = reader.GetString(4);
            var score = reader.GetInt32(5);
            var blob = title + " · " + desc + " · " + ts;
            hits.Add(new GlobalSearchHit
            {
                Module = GlobalSearchModule.Idea,
                EntityId = id,
                Title = title,
                Snippet = BuildSnippet(title, blob, needle),
                MatchScore = score,
                UpdatedAt = updated,
                Jump = new GlobalSearchJumpContext(),
            });
        }
    }

    private static string BuildSnippet(string title, string body, string needle, int maxTotal = 200)
    {
        var combined = string.IsNullOrEmpty(body) ? title : $"{title} — {body}";
        if (combined.Length <= maxTotal)
        {
            return combined;
        }

        var idx = combined.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            idx = 0;
        }

        var start = Math.Max(0, idx - 30);
        var len = Math.Min(maxTotal, combined.Length - start);
        var s = combined.Substring(start, len);
        return start > 0 ? "…" + s : s;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
        {
            return s;
        }

        return s[..max] + "…";
    }

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}

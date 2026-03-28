using System.Globalization;
using System.Text;
using MiniExcelLibs;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;
using PMTool.Core.Models.DataManagement;

namespace PMTool.Infrastructure.Export;

public sealed class DataExportService(
    IProjectRepository projectRepository,
    IFeatureRepository featureRepository,
    ITaskRepository taskRepository,
    IReleaseRepository releaseRepository,
    IDocumentRepository documentRepository,
    IIdeaRepository ideaRepository) : IDataExportService
{
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);

    public async Task ExportAsync(
        DataExportRequest request,
        IProgress<(string message, int percent)>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputDirectory);
        _ = Directory.CreateDirectory(request.OutputDirectory);

        var modules = request.Modules;
        if (modules == DataExportModule.None)
        {
            throw new InvalidOperationException("请至少选择一个导出模块。");
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var parts = new List<DataExportModule>();
        foreach (DataExportModule m in Enum.GetValues<DataExportModule>())
        {
            if (m is DataExportModule.None or DataExportModule.All)
            {
                continue;
            }

            if (modules.HasFlag(m))
            {
                parts.Add(m);
            }
        }

        var total = Math.Max(1, parts.Count);
        var done = 0;

        void Report(string msg) =>
            progress?.Report((msg, Math.Min(99, done * 100 / total)));

        if (request.Format == DataExportFormat.Excel)
        {
            var sheets = new Dictionary<string, object>();
            foreach (var m in parts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Report($"正在导出{ModuleLabel(m)}…");
                sheets[ModuleLabel(m)] = m switch
                {
                    DataExportModule.Projects => await LoadProjectsAsync(cancellationToken).ConfigureAwait(false),
                    DataExportModule.Features => await LoadFeaturesAsync(cancellationToken).ConfigureAwait(false),
                    DataExportModule.Tasks => await LoadTasksAsync(cancellationToken).ConfigureAwait(false),
                    DataExportModule.Releases => await LoadReleasesAsync(cancellationToken).ConfigureAwait(false),
                    DataExportModule.Documents => await LoadDocumentsAsync(cancellationToken).ConfigureAwait(false),
                    DataExportModule.Ideas => await LoadIdeasAsync(cancellationToken).ConfigureAwait(false),
                    _ => Array.Empty<Dictionary<string, object?>>(),
                };
                done++;
            }

            var path = Path.Combine(request.OutputDirectory, $"数据导出_{stamp}.xlsx");
            await MiniExcel.SaveAsAsync(path, sheets, cancellationToken: cancellationToken).ConfigureAwait(false);
            progress?.Report(("导出完成。", 100));
            return;
        }

        foreach (var m in parts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report($"正在导出{ModuleLabel(m)} CSV…");
            var rows = m switch
            {
                DataExportModule.Projects => await LoadProjectsAsync(cancellationToken).ConfigureAwait(false),
                DataExportModule.Features => await LoadFeaturesAsync(cancellationToken).ConfigureAwait(false),
                DataExportModule.Tasks => await LoadTasksAsync(cancellationToken).ConfigureAwait(false),
                DataExportModule.Releases => await LoadReleasesAsync(cancellationToken).ConfigureAwait(false),
                DataExportModule.Documents => await LoadDocumentsAsync(cancellationToken).ConfigureAwait(false),
                DataExportModule.Ideas => await LoadIdeasAsync(cancellationToken).ConfigureAwait(false),
                _ => [],
            };

            var fn = $"{ModuleFilePrefix(m)}_{stamp}.csv";
            await WriteCsvAsync(Path.Combine(request.OutputDirectory, fn), rows, cancellationToken)
                .ConfigureAwait(false);
            done++;
        }

        progress?.Report(("导出完成。", 100));
    }

    private static string ModuleLabel(DataExportModule m) => m switch
    {
        DataExportModule.Projects => "项目",
        DataExportModule.Features => "特性",
        DataExportModule.Tasks => "任务",
        DataExportModule.Releases => "版本",
        DataExportModule.Documents => "文档",
        DataExportModule.Ideas => "灵感池",
        _ => "其他",
    };

    private static string ModuleFilePrefix(DataExportModule m) => m switch
    {
        DataExportModule.Projects => "项目",
        DataExportModule.Features => "特性",
        DataExportModule.Tasks => "任务",
        DataExportModule.Releases => "版本",
        DataExportModule.Documents => "文档",
        DataExportModule.Ideas => "灵感池",
        _ => "导出",
    };

    private async Task<List<Dictionary<string, object?>>> LoadProjectsAsync(CancellationToken ct)
    {
        var list = await projectRepository
            .ListAsync(new ProjectListQuery(null, null, ProjectSortField.UpdatedAt, true), ct)
            .ConfigureAwait(false);
        return list.Select(p => new Dictionary<string, object?>
        {
            ["Id"] = p.Project.Id,
            ["名称"] = p.Project.Name,
            ["描述"] = p.Project.Description,
            ["状态"] = p.Project.Status,
            ["分类"] = p.Project.Category,
            ["创建时间"] = p.Project.CreatedAt,
            ["更新时间"] = p.Project.UpdatedAt,
            ["特性数"] = p.FeatureCount,
            ["任务数"] = p.TaskCount,
            ["版本数"] = p.ReleaseCount,
            ["文档数"] = p.DocumentCount,
            ["关联灵感数"] = p.LinkedIdeaCount,
            ["行版本"] = p.Project.RowVersion,
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadFeaturesAsync(CancellationToken ct)
    {
        var list = await featureRepository.ListAllActiveAsync(ct).ConfigureAwait(false);
        return list.Select(f => new Dictionary<string, object?>
        {
            ["Id"] = f.Id,
            ["项目Id"] = f.ProjectId,
            ["名称"] = f.Name,
            ["描述"] = f.Description,
            ["状态"] = f.Status,
            ["优先级"] = f.Priority,
            ["验收标准"] = f.AcceptanceCriteria,
            ["技术栈"] = f.TechStack,
            ["备注"] = f.Notes,
            ["截止日期"] = f.DueDate,
            ["附件占位"] = f.AttachmentsPlaceholder,
            ["创建时间"] = f.CreatedAt,
            ["更新时间"] = f.UpdatedAt,
            ["行版本"] = f.RowVersion,
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadTasksAsync(CancellationToken ct)
    {
        var list = await taskRepository.ListAllActiveAsync(ct).ConfigureAwait(false);
        return list.Select(t => new Dictionary<string, object?>
        {
            ["Id"] = t.Id,
            ["项目Id"] = t.ProjectId,
            ["特性Id"] = t.FeatureId,
            ["名称"] = t.Name,
            ["描述"] = t.Description,
            ["类型"] = t.TaskType,
            ["状态"] = t.Status,
            ["严重级别"] = t.Severity,
            ["预估工时"] = t.EstimatedHours,
            ["实际工时"] = t.ActualHours,
            ["完成时间"] = t.CompletedAt,
            ["排序"] = t.SortValue,
            ["创建时间"] = t.CreatedAt,
            ["更新时间"] = t.UpdatedAt,
            ["行版本"] = t.RowVersion,
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadReleasesAsync(CancellationToken ct)
    {
        var list = await releaseRepository.ListAllActiveAsync(ct).ConfigureAwait(false);
        return list.Select(r => new Dictionary<string, object?>
        {
            ["Id"] = r.Id,
            ["项目Id"] = r.ProjectId,
            ["名称"] = r.Name,
            ["描述"] = r.Description,
            ["开始"] = r.StartAt,
            ["结束"] = r.EndAt,
            ["状态"] = r.Status,
            ["创建时间"] = r.CreatedAt,
            ["更新时间"] = r.UpdatedAt,
            ["行版本"] = r.RowVersion,
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadDocumentsAsync(CancellationToken ct)
    {
        var list = await documentRepository.ListActiveAsync(ct).ConfigureAwait(false);
        return list.Select(d => new Dictionary<string, object?>
        {
            ["Id"] = d.Id,
            ["项目Id"] = d.ProjectId,
            ["特性Id"] = d.FeatureId,
            ["名称"] = d.Name,
            ["关联类型"] = d.RelateType,
            ["正文"] = d.Content,
            ["格式"] = d.ContentFormat,
            ["代码片段"] = d.IsCodeSnippet,
            ["创建时间"] = d.CreatedAt,
            ["更新时间"] = d.UpdatedAt,
            ["行版本"] = d.RowVersion,
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadIdeasAsync(CancellationToken ct)
    {
        var list = await ideaRepository
            .ListAsync(new IdeaListQuery(null, null, null, IdeaSortField.UpdatedAt, true), ct)
            .ConfigureAwait(false);
        return list.Select(i => new Dictionary<string, object?>
        {
            ["Id"] = i.Id,
            ["标题"] = i.Title,
            ["描述"] = i.Description,
            ["技术栈"] = i.TechStack,
            ["状态"] = i.Status,
            ["优先级"] = i.Priority,
            ["关联项目Id"] = i.LinkedProjectId,
            ["创建时间"] = i.CreatedAt,
            ["更新时间"] = i.UpdatedAt,
            ["行版本"] = i.RowVersion,
        }).ToList();
    }

    private static async Task WriteCsvAsync(
        string path,
        IReadOnlyList<Dictionary<string, object?>> rows,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream, Utf8Bom);
        if (rows.Count == 0)
        {
            await writer.WriteLineAsync("无数据").ConfigureAwait(false);
            return;
        }

        var keys = rows[0].Keys.ToList();
        await writer.WriteLineAsync(string.Join(",", keys.Select(EscapeCsv))).ConfigureAwait(false);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = string.Join(",", keys.Select(k => EscapeCsv(FormatCell(row.GetValueOrDefault(k)))));
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    private static string FormatCell(object? v) => v switch
    {
        null => "",
        bool b => b ? "1" : "0",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "",
        _ => v.ToString() ?? "",
    };

    private static string EscapeCsv(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "\"\"";
        }

        if (s.Contains('"', StringComparison.Ordinal))
        {
            s = s.Replace("\"", "\"\"", StringComparison.Ordinal);
        }

        if (s.Contains(',', StringComparison.Ordinal) || s.Contains('\n', StringComparison.Ordinal) ||
            s.Contains('\r', StringComparison.Ordinal))
        {
            return $"\"{s}\"";
        }

        return s;
    }
}

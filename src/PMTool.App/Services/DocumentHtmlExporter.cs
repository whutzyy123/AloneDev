using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using PMTool.Core.IO;

namespace PMTool.App.Services;

internal static partial class DocumentHtmlExporter
{
    internal static async Task ExportMarkdownToHtmlFileAsync(
        string accountRoot,
        string documentTitle,
        string markdown,
        string targetHtmlPath,
        CancellationToken cancellationToken = default)
    {
        var pipeline = new MarkdownPipelineBuilder().DisableHtml().Build();
        var htmlFragment = Markdown.ToHtml(markdown ?? string.Empty, pipeline);
        var dir = Path.GetDirectoryName(targetHtmlPath)!;
        var baseName = Path.GetFileNameWithoutExtension(targetHtmlPath);
        var filesDirName = baseName + "_files";
        var filesDir = Path.Combine(dir, filesDirName);
        Directory.CreateDirectory(filesDir);
        var adjusted = await ProcessImgSrcAsync(accountRoot, htmlFragment, filesDirName, filesDir, cancellationToken)
            .ConfigureAwait(false);
        var titleEnc = System.Net.WebUtility.HtmlEncode(documentTitle);
        const string Css =
            "body{font-family:'Segoe UI',system-ui,sans-serif;max-width:900px;margin:24px auto;padding:0 16px;line-height:1.55;}"
            + "pre{background:#f4f4f4;padding:12px;border-radius:8px;overflow:auto;}img{max-width:100%;height:auto;}";
        var full =
            "<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"/><title>"
            + titleEnc
            + "</title><style>"
            + Css
            + "</style></head><body>"
            + adjusted
            + "</body></html>";
        await File.WriteAllTextAsync(targetHtmlPath, full, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ProcessImgSrcAsync(
        string accountRoot,
        string html,
        string filesDirName,
        string filesDirAbsolute,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = ImgSrcRegex().Matches(html);
        var idx = 0;
        foreach (Match m in matches)
        {
            var src = m.Groups[1].Value.Trim();
            if (map.ContainsKey(src))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var abs = ResolveUnderAccount(accountRoot, src);
            if (abs is null || !File.Exists(abs))
            {
                continue;
            }

            idx++;
            var ext = Path.GetExtension(abs);
            if (string.IsNullOrEmpty(ext))
            {
                ext = ".bin";
            }

            var destShort = $"img{idx}{ext}";
            var destPath = Path.Combine(filesDirAbsolute, destShort);
            await Task.Run(() => File.Copy(abs, destPath, overwrite: true), cancellationToken).ConfigureAwait(false);
            map[src] = filesDirName + "/" + destShort.Replace('\\', '/');
        }

        var result = html;
        foreach (var kv in map)
        {
            result = result.Replace($"src=\"{kv.Key}\"", $"src=\"{kv.Value}\"", StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string? ResolveUnderAccount(string accountRoot, string src)
    {
        if (string.IsNullOrWhiteSpace(src))
        {
            return null;
        }

        var normalized = src.Replace('/', Path.DirectorySeparatorChar);
        try
        {
            if (Path.IsPathRooted(normalized))
            {
                return File.Exists(normalized) ? normalized : null;
            }

            var combined = Path.GetFullPath(Path.Combine(accountRoot, normalized));
            if (!PathSecurity.IsPathWithinDirectory(accountRoot, combined))
            {
                return null;
            }

            return File.Exists(combined) ? combined : null;
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex("src=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcRegex();
}

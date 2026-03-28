using System.Collections.Frozen;
using System.Linq;
using PMTool.Core.Models;

namespace PMTool.Core.Validation;

public static class DocumentFieldValidator
{
    private const int ContentMaxLength = 2_000_000;

    private static readonly FrozenSet<string> AllowedSnippetLanguagesInternal = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "plaintext",
        "bash",
        "c",
        "csharp",
        "cpp",
        "css",
        "dart",
        "diff",
        "dockerfile",
        "fs",
        "fsharp",
        "go",
        "graphql",
        "html",
        "ini",
        "java",
        "javascript",
        "json",
        "kotlin",
        "less",
        "lua",
        "markdown",
        "php",
        "powershell",
        "python",
        "ruby",
        "rust",
        "scss",
        "shell",
        "sql",
        "swift",
        "typescript",
        "vbnet",
        "xml",
        "yaml",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>供 UI 下拉框使用的稳定排序列表（与 <see cref="NormalizeSnippetLanguageForStorage"/> 白名单一致）。</summary>
    public static IReadOnlyList<string> SnippetLanguagePickerOptions { get; } =
        AllowedSnippetLanguagesInternal.OrderBy(static s => s, StringComparer.Ordinal).ToArray();

    public static string ValidateName(string name) => SharedEntityNameRules.Validate(name, "文档");

    public static string ValidateContent(string? content)
    {
        var s = content ?? string.Empty;
        if (s.Length > ContentMaxLength)
        {
            throw new ArgumentException($"文档正文不可超过 {ContentMaxLength} 个字符。", nameof(content));
        }

        return s;
    }

    /// <summary>校验关联字段与 <see cref="DocumentRelateTypes"/> 一致。</summary>
    public static void ValidateRelation(string relateType, string? projectId, string? featureId)
    {
        switch (relateType)
        {
            case DocumentRelateTypes.Global:
                if (projectId is not null || featureId is not null)
                {
                    throw new ArgumentException("全局文档不可关联项目或模块。");
                }

                break;
            case DocumentRelateTypes.Project:
                if (string.IsNullOrWhiteSpace(projectId) || featureId is not null)
                {
                    throw new ArgumentException("项目文档必须指定项目，且不可指定模块。");
                }

                break;
            case DocumentRelateTypes.Feature:
                if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(featureId))
                {
                    throw new ArgumentException("模块文档必须同时指定项目与模块。");
                }

                break;
            default:
                throw new ArgumentException("未知的文档关联类型。");
        }
    }

    public static string ValidateContentFormat(string? format)
    {
        var f = (format ?? string.Empty).Trim();
        if (f is not (DocumentContentFormats.Markdown or DocumentContentFormats.PlainText))
        {
            throw new ArgumentException("正文格式仅支持 Markdown 或 PlainText。", nameof(format));
        }

        return f;
    }

    /// <summary>写入 SQLite 前规范化片段语言；非片段固定为 <c>null</c>。</summary>
    public static string? NormalizeSnippetLanguageForStorage(string? language, bool isCodeSnippet)
    {
        if (!isCodeSnippet)
        {
            return null;
        }

        var s = (language ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(s))
        {
            return "plaintext";
        }

        if (!AllowedSnippetLanguagesInternal.Contains(s))
        {
            throw new ArgumentException($"不支持的代码高亮语言：{s}", nameof(language));
        }

        return s.ToLowerInvariant();
    }

    public static void ValidateForInsert(PmDocument document)
    {
        _ = ValidateName(document.Name);
        _ = ValidateContent(document.Content);
        _ = ValidateContentFormat(document.ContentFormat);
        _ = NormalizeSnippetLanguageForStorage(document.SnippetLanguage, document.IsCodeSnippet);
        ValidateRelation(document.RelateType, document.ProjectId, document.FeatureId);
    }
}

using PMTool.Core.Models;

namespace PMTool.Core.Validation;

public static class DocumentFieldValidator
{
    private const int ContentMaxLength = 2_000_000;

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
                    throw new ArgumentException("全局文档不可关联项目或特性。");
                }

                break;
            case DocumentRelateTypes.Project:
                if (string.IsNullOrWhiteSpace(projectId) || featureId is not null)
                {
                    throw new ArgumentException("项目文档必须指定项目，且不可指定特性。");
                }

                break;
            case DocumentRelateTypes.Feature:
                if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(featureId))
                {
                    throw new ArgumentException("特性文档必须同时指定项目与特性。");
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

    public static void ValidateForInsert(PmDocument document)
    {
        _ = ValidateName(document.Name);
        _ = ValidateContent(document.Content);
        _ = ValidateContentFormat(document.ContentFormat);
        ValidateRelation(document.RelateType, document.ProjectId, document.FeatureId);
    }
}

using PMTool.Core.Models;
using PMTool.Core;

namespace PMTool.Core.Validation;

public static class IdeaFieldValidator
{
    private const int DescriptionMax = 1000;

    private const int TechStackMax = 100;

    public static string ValidateTitle(string title) => SharedEntityNameRules.Validate(title, "灵感");

    public static string ValidateDescription(string? description)
    {
        var s = description ?? string.Empty;
        if (s.Length > DescriptionMax)
        {
            throw new ArgumentException($"灵感描述不可超过 {DescriptionMax} 个字符。", nameof(description));
        }

        return s;
    }

    public static string ValidateTechStack(string? techStack)
    {
        var s = techStack ?? string.Empty;
        if (s.Length > TechStackMax)
        {
            throw new ArgumentException($"技术栈不可超过 {TechStackMax} 个字符。", nameof(techStack));
        }

        return s;
    }

    public static string ValidateStatus(string? status)
    {
        var s = (status ?? string.Empty).Trim();
        if (!IdeaStatuses.IsKnown(s))
        {
            throw new ArgumentException("灵感状态无效。", nameof(status));
        }

        return s;
    }

    public static string? ValidatePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return null;
        }

        var p = priority.Trim();
        if (!IdeaPriorities.IsKnown(p))
        {
            throw new ArgumentException("优先级仅可为 P0、P1、P2、P3 或留空。", nameof(priority));
        }

        return p;
    }

    public static void ValidateForInsert(Idea idea)
    {
        _ = ValidateTitle(idea.Title);
        _ = ValidateDescription(idea.Description);
        _ = ValidateTechStack(idea.TechStack);
        _ = ValidateStatus(idea.Status);
        _ = ValidatePriority(idea.Priority);
        ValidateLinkedProject(idea.Status, idea.LinkedProjectId);
    }

    /// <summary>非「已立项」时不可保留关联项目。</summary>
    public static void ValidateLinkedProject(string status, string? linkedProjectId)
    {
        if (status != IdeaStatuses.Approved && linkedProjectId is { Length: > 0 })
        {
            throw new ArgumentException("仅「已立项」状态可关联项目，请先切换状态或清除关联项目。");
        }
    }

    /// <summary>持久化前规范化：<see cref="IdeaStatuses.Approved"/> 外一律清空关联。</summary>
    public static string? NormalizeLinkedProjectId(string status, string? linkedProjectId) =>
        status == IdeaStatuses.Approved ? linkedProjectId : null;
}

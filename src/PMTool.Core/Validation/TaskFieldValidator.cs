namespace PMTool.Core.Validation;

public static class TaskFieldValidator
{
    public static string ValidateName(string name) => SharedEntityNameRules.Validate(name, "任务");

    public static string ValidateDescription(string? description)
    {
        var s = description ?? string.Empty;
        if (s.Length > 500)
        {
            throw new ArgumentException("任务描述不可超过 500 个字符。", nameof(description));
        }

        return s;
    }

    public static double ValidateEstimatedHours(double hours)
    {
        if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hours), "预估工时不可为负。");
        }

        if (hours > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(hours), "预估工时不可超过 999 小时。");
        }

        return Math.Round(hours, 1, MidpointRounding.AwayFromZero);
    }

    public static double ValidateActualHours(double hours)
    {
        if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hours), "实际工时不可为负。");
        }

        if (hours > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(hours), "实际工时不可超过 999 小时。");
        }

        return Math.Round(hours, 1, MidpointRounding.AwayFromZero);
    }
}

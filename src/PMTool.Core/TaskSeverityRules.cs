namespace PMTool.Core;

public static class TaskSeverityRules
{
    public static string? NormalizeForPersistence(string taskType, string? severity)
    {
        if (taskType != TaskTypes.Bug)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            return TaskSeverities.Major;
        }

        var s = severity.Trim();
        return TaskSeverities.All.Contains(s) ? s : TaskSeverities.Major;
    }

    public static bool TryValidate(string taskType, string? severity, out string? errorMessage)
    {
        errorMessage = null;
        if (taskType != TaskTypes.Bug)
        {
            return severity is null or "";
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            return true;
        }

        if (!TaskSeverities.All.Contains(severity))
        {
            errorMessage = "无效的严重程度。";
            return false;
        }

        return true;
    }
}

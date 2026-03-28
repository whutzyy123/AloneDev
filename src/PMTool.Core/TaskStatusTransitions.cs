namespace PMTool.Core;

/// <summary>任务状态：未开始与进行中可互换；进行中可到已完成/已取消；已完成与已取消可回退到进行中或未开始。</summary>
public static class TaskStatusTransitions
{
    public static bool TryValidate(string? fromStatus, string toStatus, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrEmpty(toStatus))
        {
            errorMessage = "状态不可为空。";
            return false;
        }

        if (fromStatus == toStatus)
        {
            return true;
        }

        if (!TaskStatuses.All.Contains(toStatus))
        {
            errorMessage = "未知的目标状态。";
            return false;
        }

        if (string.IsNullOrEmpty(fromStatus))
        {
            errorMessage = "无法从空状态变更。";
            return false;
        }

        if (!TaskStatuses.All.Contains(fromStatus))
        {
            errorMessage = "未知的当前状态。";
            return false;
        }

        if (IsAllowed(fromStatus, toStatus))
        {
            return true;
        }

        errorMessage = $"不允许从「{fromStatus}」变更为「{toStatus}」。";
        return false;
    }

    public static IReadOnlyList<string> GetAllowedTargets(string currentStatus)
    {
        var list = new List<string>();
        foreach (var s in TaskStatuses.All)
        {
            if (TryValidate(currentStatus, s, out _))
            {
                list.Add(s);
            }
        }

        return list;
    }

    private static bool IsAllowed(string from, string to) => (from, to) switch
    {
        (TaskStatuses.NotStarted, TaskStatuses.InProgress) => true,
        (TaskStatuses.InProgress, TaskStatuses.NotStarted) => true,
        (TaskStatuses.InProgress, TaskStatuses.Done) => true,
        (TaskStatuses.InProgress, TaskStatuses.Cancelled) => true,
        (TaskStatuses.Done, TaskStatuses.InProgress) => true,
        (TaskStatuses.Done, TaskStatuses.NotStarted) => true,
        (TaskStatuses.Cancelled, TaskStatuses.InProgress) => true,
        (TaskStatuses.Cancelled, TaskStatuses.NotStarted) => true,
        _ => false,
    };
}

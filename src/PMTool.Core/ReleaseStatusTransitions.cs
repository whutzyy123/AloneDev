namespace PMTool.Core;

/// <summary>版本状态：仅未开始可编辑/删除；未开始→进行中；进行中→已结束/已取消；终态不可回退。</summary>
public static class ReleaseStatusTransitions
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

        if (!ReleaseStatuses.All.Contains(toStatus))
        {
            errorMessage = "未知的目标状态。";
            return false;
        }

        if (string.IsNullOrEmpty(fromStatus))
        {
            errorMessage = "无法从空状态变更。";
            return false;
        }

        if (!ReleaseStatuses.All.Contains(fromStatus))
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

    public static bool IsTerminal(string status) =>
        status is ReleaseStatuses.Ended or ReleaseStatuses.Cancelled;

    public static bool CanEditOrDelete(string status) => status == ReleaseStatuses.NotStarted;

    /// <summary>进行中时可手动结束。</summary>
    public static bool CanFinishOrCancel(string status) => status == ReleaseStatuses.InProgress;

    private static bool IsAllowed(string from, string to) => (from, to) switch
    {
        (ReleaseStatuses.NotStarted, ReleaseStatuses.InProgress) => true,
        (ReleaseStatuses.InProgress, ReleaseStatuses.Ended) => true,
        (ReleaseStatuses.InProgress, ReleaseStatuses.Cancelled) => true,
        _ => false,
    };
}

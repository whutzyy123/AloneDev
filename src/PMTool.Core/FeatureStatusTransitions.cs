namespace PMTool.Core;

/// <summary>特性状态转移规则：仅允许相邻前进；已完成/已上线可退回进行中。</summary>
public static class FeatureStatusTransitions
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

        if (!FeatureStatuses.All.Contains(toStatus))
        {
            errorMessage = "未知的目标状态。";
            return false;
        }

        if (string.IsNullOrEmpty(fromStatus))
        {
            errorMessage = "无法从空状态变更。";
            return false;
        }

        if (!FeatureStatuses.All.Contains(fromStatus))
        {
            errorMessage = "未知的当前状态。";
            return false;
        }

        if (IsAdjacentForward(fromStatus, toStatus))
        {
            return true;
        }

        if (IsRollbackToInProgress(fromStatus, toStatus))
        {
            return true;
        }

        errorMessage = $"不允许从「{fromStatus}」变更为「{toStatus}」。";
        return false;
    }

    public static IReadOnlyList<string> GetAllowedTargets(string currentStatus)
    {
        var list = new List<string>();
        foreach (var s in FeatureStatuses.All)
        {
            if (TryValidate(currentStatus, s, out _))
            {
                list.Add(s);
            }
        }

        return list;
    }

    private static bool IsAdjacentForward(string from, string to) => (from, to) switch
    {
        (FeatureStatuses.ToPlan, FeatureStatuses.InProgress) => true,
        (FeatureStatuses.InProgress, FeatureStatuses.Done) => true,
        (FeatureStatuses.Done, FeatureStatuses.Released) => true,
        _ => false,
    };

    private static bool IsRollbackToInProgress(string from, string to) =>
        to == FeatureStatuses.InProgress && (from == FeatureStatuses.Done || from == FeatureStatuses.Released);
}

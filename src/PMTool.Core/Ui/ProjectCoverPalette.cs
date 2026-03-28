namespace PMTool.Core.Ui;

/// <summary>项目卡片封面渐变：由项目 Id 稳定映射到调色板下标（离线、无随机）。</summary>
public static class ProjectCoverPalette
{
    public const int GradientCount = 8;

    public static int GetStableIndex(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            return 0;
        }

        unchecked
        {
            var hash = 5381;
            foreach (var c in projectId)
            {
                hash = ((hash << 5) + hash) + c;
            }

            var nonNegative = hash & int.MaxValue;
            return nonNegative % GradientCount;
        }
    }
}

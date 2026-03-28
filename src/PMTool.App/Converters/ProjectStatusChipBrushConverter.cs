using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using PMTool.Core;

namespace PMTool.App.Converters;

/// <summary>项目状态胶囊底色：进行中偏绿，其它偏中性灰。</summary>
public sealed class ProjectStatusChipBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var resources = Microsoft.UI.Xaml.Application.Current?.Resources;
        if (resources is null)
        {
            return new SolidColorBrush(Microsoft.UI.Colors.LightGray);
        }

        var key = value is string s && s == ProjectStatuses.InProgress
            ? "AloneChipProjectActiveBackgroundBrush"
            : "AloneChipProjectMutedBackgroundBrush";

        if (resources.TryGetValue(key, out var b) && b is Brush br)
        {
            return br;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.LightGray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

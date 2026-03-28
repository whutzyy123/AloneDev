using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using PMTool.Core;

namespace PMTool.App.Converters;

/// <summary>项目状态胶囊：背景（进行中亮青底）或前景（进行中深青字）。ConverterParameter="Foreground" 时返回文字色。</summary>
public sealed class ProjectStatusChipBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var resources = Microsoft.UI.Xaml.Application.Current?.Resources;
        if (resources is null)
        {
            return new SolidColorBrush(Microsoft.UI.Colors.LightGray);
        }

        var isActive = value is string s && s == ProjectStatuses.InProgress;
        var foreground = parameter is string p && string.Equals(p, "Foreground", StringComparison.OrdinalIgnoreCase);

        var key = (foreground, isActive) switch
        {
            (true, true) => "AloneChipProjectActiveForegroundBrush",
            (true, false) => "AloneOnSurfaceVariantBrush",
            (false, true) => "AloneChipProjectActiveBackgroundBrush",
            _ => "AloneChipProjectMutedBackgroundBrush",
        };

        if (resources.TryGetValue(key, out var b) && b is Brush br)
        {
            return br;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.LightGray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

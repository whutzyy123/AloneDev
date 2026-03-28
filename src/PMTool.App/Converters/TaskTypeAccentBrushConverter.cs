using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using PMTool.Core;

namespace PMTool.App.Converters;

/// <summary>任务类型左侧色条：Bug 警示红、Refactor 极客紫、Research 深海蓝（低饱和）。</summary>
public sealed class TaskTypeAccentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var resources = Microsoft.UI.Xaml.Application.Current?.Resources;
        if (resources is null)
        {
            return new SolidColorBrush(Microsoft.UI.Colors.SteelBlue);
        }

        var key = (value as string) switch
        {
            TaskTypes.Bug => "AloneDevAccentBugBrushGradient",
            TaskTypes.Refactor => "AloneDevAccentRefactorBrush",
            TaskTypes.Research => "AloneDevAccentResearchBrush",
            _ => "AloneDevAccentFeatureBrush",
        };

        if (resources.TryGetValue(key, out var b) && b is Brush br)
        {
            return br;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.SteelBlue);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

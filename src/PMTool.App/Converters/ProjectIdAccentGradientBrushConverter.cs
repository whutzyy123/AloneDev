using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using PMTool.Core.Ui;

namespace PMTool.App.Converters;

public sealed class ProjectIdAccentGradientBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var resources = Microsoft.UI.Xaml.Application.Current?.Resources;
        if (resources is null)
        {
            return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        var id = value?.ToString() ?? string.Empty;
        var idx = ProjectCoverPalette.GetStableIndex(id);
        var key = $"AloneProjectCoverGradient{idx}";
        if (resources.TryGetValue(key, out var o) && o is Brush br)
        {
            return br;
        }

        return resources.TryGetValue("AloneProjectCoverGradient0", out var fb) && fb is Brush fbBr
            ? fbBr
            : resources.TryGetValue("AloneSurfaceContainerHighBrush", out var fb2) && fb2 is Brush fb2Br
                ? fb2Br
                : new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

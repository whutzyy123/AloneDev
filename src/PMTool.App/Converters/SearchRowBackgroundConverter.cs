using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PMTool.App.Converters;

public sealed class SearchRowBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var resources = Microsoft.UI.Xaml.Application.Current?.Resources;
        if (resources is null)
        {
            return new SolidColorBrush(Microsoft.UI.Colors.WhiteSmoke);
        }

        if (value is true
            && resources.TryGetValue("SearchRowHighlightBrush", out var hi)
            && hi is Brush hiBrush)
        {
            return hiBrush;
        }

        return resources.TryGetValue("AloneSurfaceContainerLowestBrush", out var def) && def is Brush d
            ? d
            : new SolidColorBrush(Microsoft.UI.Colors.WhiteSmoke);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

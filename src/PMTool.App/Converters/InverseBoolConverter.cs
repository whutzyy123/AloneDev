using Microsoft.UI.Xaml.Data;

namespace PMTool.App.Converters;

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is bool b ? !b : value;
}

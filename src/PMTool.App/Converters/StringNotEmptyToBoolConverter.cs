using Microsoft.UI.Xaml.Data;

namespace PMTool.App.Converters;

public sealed class StringNotEmptyToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is string s && s.Length > 0;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

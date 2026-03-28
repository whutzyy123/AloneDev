using Microsoft.UI.Xaml.Controls;

namespace PMTool.App.Controls;

public sealed partial class OperationBar : UserControl
{
    public OperationBar()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(OperationBar),
        new PropertyMetadata(string.Empty));
}

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

    public string FilterLabel
    {
        get => (string)GetValue(FilterLabelProperty);
        set => SetValue(FilterLabelProperty, value);
    }

    public static readonly DependencyProperty FilterLabelProperty = DependencyProperty.Register(
        nameof(FilterLabel),
        typeof(string),
        typeof(OperationBar),
        new PropertyMetadata("筛选"));

    public string PrimaryActionLabel
    {
        get => (string)GetValue(PrimaryActionLabelProperty);
        set => SetValue(PrimaryActionLabelProperty, value);
    }

    public static readonly DependencyProperty PrimaryActionLabelProperty = DependencyProperty.Register(
        nameof(PrimaryActionLabel),
        typeof(string),
        typeof(OperationBar),
        new PropertyMetadata("新建"));
}

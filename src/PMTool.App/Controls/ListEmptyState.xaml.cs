using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PMTool.App.Controls;

public sealed partial class ListEmptyState : UserControl
{
    public ListEmptyState()
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
        typeof(ListEmptyState),
        new PropertyMetadata(string.Empty));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(ListEmptyState),
        new PropertyMetadata(string.Empty));

    /// <summary>Segoe MDL2 glyph string, e.g. inbox icon.</summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(ListEmptyState),
        new PropertyMetadata("\uE7C3"));

    public string PrimaryLabel
    {
        get => (string)GetValue(PrimaryLabelProperty);
        set => SetValue(PrimaryLabelProperty, value);
    }

    public static readonly DependencyProperty PrimaryLabelProperty = DependencyProperty.Register(
        nameof(PrimaryLabel),
        typeof(string),
        typeof(ListEmptyState),
        new PropertyMetadata(string.Empty));

    public ICommand? PrimaryCommand
    {
        get => (ICommand?)GetValue(PrimaryCommandProperty);
        set => SetValue(PrimaryCommandProperty, value);
    }

    public static readonly DependencyProperty PrimaryCommandProperty = DependencyProperty.Register(
        nameof(PrimaryCommand),
        typeof(ICommand),
        typeof(ListEmptyState),
        new PropertyMetadata(null));

    public bool IsPrimaryVisible
    {
        get => (bool)GetValue(IsPrimaryVisibleProperty);
        set => SetValue(IsPrimaryVisibleProperty, value);
    }

    public static readonly DependencyProperty IsPrimaryVisibleProperty = DependencyProperty.Register(
        nameof(IsPrimaryVisible),
        typeof(bool),
        typeof(ListEmptyState),
        new PropertyMetadata(true));
}

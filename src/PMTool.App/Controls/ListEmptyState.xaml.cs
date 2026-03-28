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

    public string Footnote
    {
        get => (string)GetValue(FootnoteProperty);
        set => SetValue(FootnoteProperty, value);
    }

    public static readonly DependencyProperty FootnoteProperty = DependencyProperty.Register(
        nameof(Footnote),
        typeof(string),
        typeof(ListEmptyState),
        new PropertyMetadata(string.Empty));

    public double IconFontSize
    {
        get => (double)GetValue(IconFontSizeProperty);
        set => SetValue(IconFontSizeProperty, value);
    }

    public static readonly DependencyProperty IconFontSizeProperty = DependencyProperty.Register(
        nameof(IconFontSize),
        typeof(double),
        typeof(ListEmptyState),
        new PropertyMetadata(44d));

    public Thickness IconBadgePadding
    {
        get => (Thickness)GetValue(IconBadgePaddingProperty);
        set => SetValue(IconBadgePaddingProperty, value);
    }

    public static readonly DependencyProperty IconBadgePaddingProperty = DependencyProperty.Register(
        nameof(IconBadgePadding),
        typeof(Thickness),
        typeof(ListEmptyState),
        new PropertyMetadata(new Thickness(22)));

    public double IconOpacity
    {
        get => (double)GetValue(IconOpacityProperty);
        set => SetValue(IconOpacityProperty, value);
    }

    public static readonly DependencyProperty IconOpacityProperty = DependencyProperty.Register(
        nameof(IconOpacity),
        typeof(double),
        typeof(ListEmptyState),
        new PropertyMetadata(1d));

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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace PMTool.App.Controls;

public sealed partial class HighlightedSnippetBlock : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(HighlightedSnippetBlock),
        new PropertyMetadata(string.Empty, OnPropertyChanged));

    public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register(
        nameof(Highlight),
        typeof(string),
        typeof(HighlightedSnippetBlock),
        new PropertyMetadata(null, OnPropertyChanged));

    public HighlightedSnippetBlock()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Highlight
    {
        get => (string?)GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
    {
        if (d is HighlightedSnippetBlock block)
        {
            block.Rebuild();
        }
    }

    private void Rebuild()
    {
        RichRoot.Blocks.Clear();
        var text = Text ?? string.Empty;
        var h = Highlight;
        var paragraph = new Paragraph();

        if (string.IsNullOrEmpty(h))
        {
            if (text.Length > 0)
            {
                paragraph.Inlines.Add(new Run { Text = text });
            }
        }
        else
        {
            var idx = 0;
            while (idx < text.Length)
            {
                var found = text.IndexOf(h, idx, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    if (idx < text.Length)
                    {
                        paragraph.Inlines.Add(new Run { Text = text[idx..] });
                    }

                    break;
                }

                if (found > idx)
                {
                    paragraph.Inlines.Add(new Run { Text = text.Substring(idx, found - idx) });
                }

                var matchLen = Math.Min(h.Length, text.Length - found);
                var accent = Microsoft.UI.Xaml.Application.Current.Resources["AlonePrimaryBrush"] as Brush ?? RichRoot.Foreground;
                paragraph.Inlines.Add(new Run
                {
                    Text = text.Substring(found, matchLen),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = accent,
                });
                idx = found + matchLen;
            }
        }

        if (paragraph.Inlines.Count > 0)
        {
            RichRoot.Blocks.Add(paragraph);
        }
    }
}

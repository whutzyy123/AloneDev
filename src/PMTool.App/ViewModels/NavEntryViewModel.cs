using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;

namespace PMTool.App.ViewModels;

public partial class NavEntryViewModel : ObservableObject
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public Symbol Glyph { get; init; } = Symbol.Document;

    /// <summary>底栏等：仅图标横向排列，完整标签放进 ToolTip。</summary>
    public bool IconOnly { get; init; }

    public bool ShowExpandedNav => !IconOnly;

    [ObservableProperty]
    private bool _isActive;

    public double RowHighlightOpacity => IsActive ? 1.0 : 0.0;

    public FontWeight NavLabelFontWeight =>
        IsActive ? new FontWeight(600) : new FontWeight(400);

    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(RowHighlightOpacity));
        OnPropertyChanged(nameof(NavLabelFontWeight));
    }
}

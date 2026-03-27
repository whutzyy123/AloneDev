using Microsoft.UI.Xaml.Controls;

namespace PMTool.App.ViewModels;

public partial class NavEntryViewModel : ObservableObject
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public Symbol Glyph { get; init; } = Symbol.Document;

    [ObservableProperty]
    private bool _isActive;

    public double AccentOpacity => IsActive ? 1.0 : 0.0;

    public double RowHighlightOpacity => IsActive ? 0.45 : 0.0;

    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(AccentOpacity));
        OnPropertyChanged(nameof(RowHighlightOpacity));
    }
}

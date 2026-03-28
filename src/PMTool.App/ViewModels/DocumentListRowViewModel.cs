using Microsoft.UI.Xaml;
using PMTool.Core.Models;

namespace PMTool.App.ViewModels;

public sealed partial class DocumentListRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSearchHighlight;

    public bool IsSectionHeader { get; init; }

    public string? SectionTitle { get; init; }

    public PmDocument? Document { get; init; }

    /// <summary>副标题，如所属项目/特性。</summary>
    public string Subtitle { get; init; } = string.Empty;

    public string PrimaryText =>
        IsSectionHeader ? (SectionTitle ?? string.Empty) : (Document?.Name ?? string.Empty);

    public double TitleFontSize => IsSectionHeader ? 15 : 14;

    public Visibility SectionHeaderVisibility => IsSectionHeader ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DocumentRowVisibility => IsSectionHeader ? Visibility.Collapsed : Visibility.Visible;
}

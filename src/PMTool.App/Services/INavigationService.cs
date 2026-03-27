using Microsoft.UI.Xaml.Controls;

namespace PMTool.App.Services;

public interface INavigationService
{
    Frame? ContentFrame { get; set; }

    bool NavigateTo(Type pageType, object? parameter = null);
}

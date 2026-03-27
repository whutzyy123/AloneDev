using Microsoft.UI.Xaml.Controls;

namespace PMTool.App.Services;

public sealed class NavigationService : INavigationService
{
    public Frame? ContentFrame { get; set; }

    public bool NavigateTo(Type pageType, object? parameter = null)
    {
        if (ContentFrame is null || !typeof(Page).IsAssignableFrom(pageType))
        {
            return false;
        }

        if (parameter is null)
        {
            return ContentFrame.Navigate(pageType);
        }

        return ContentFrame.Navigate(pageType, parameter);
    }
}

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

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

        bool ok = false;
        var transition = new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromRight,
        };

        try
        {
            ok = parameter is null
                ? ContentFrame.Navigate(pageType, null, transition)
                : ContentFrame.Navigate(pageType, parameter, transition);
        }
        catch
        {
            throw;
        }

        return ok;
    }
}

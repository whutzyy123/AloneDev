using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using PMTool.App.Diagnostics;

namespace PMTool.App.Services;

public sealed class NavigationService : INavigationService
{
    public Frame? ContentFrame { get; set; }

    public bool NavigateTo(Type pageType, object? parameter = null)
    {
        if (ContentFrame is null || !typeof(Page).IsAssignableFrom(pageType))
        {
            // #region agent log
            DebugAgentLog.Write(
                "N",
                "NavigationService.NavigateTo",
                "early exit",
                new Dictionary<string, string>
                {
                    ["frameNull"] = (ContentFrame is null).ToString(),
                    ["type"] = pageType.FullName ?? "",
                    ["assignable"] = typeof(Page).IsAssignableFrom(pageType).ToString(),
                });
            // #endregion
            return false;
        }

        // #region agent log
        DebugAgentLog.Write(
            "N",
            "NavigationService.NavigateTo",
            "before ContentFrame.Navigate",
            new Dictionary<string, string> { ["page"] = pageType.Name });
        // #endregion
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
        catch (Exception ex)
        {
            // #region agent log
            DebugAgentLog.Write(
                "N",
                "NavigationService.NavigateTo",
                "Navigate threw",
                new Dictionary<string, string> { ["page"] = pageType.Name, ["msg"] = ex.Message });
            // #endregion
            throw;
        }

        // #region agent log
        DebugAgentLog.Write(
            "N",
            "NavigationService.NavigateTo",
            ok ? "Navigate ok" : "Navigate returned false",
            new Dictionary<string, string> { ["page"] = pageType.Name, ["ok"] = ok.ToString() });
        // #endregion
        return ok;
    }
}

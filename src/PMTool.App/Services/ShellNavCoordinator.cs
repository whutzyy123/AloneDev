using PMTool.App.ViewModels;

namespace PMTool.App.Services;

public sealed class ShellNavCoordinator(ShellViewModel shellViewModel) : IShellNavCoordinator
{
    public void SelectFooterNav(string navKey) => shellViewModel.SelectFooterNav(navKey);
}

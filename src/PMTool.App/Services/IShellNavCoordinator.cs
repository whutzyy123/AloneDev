namespace PMTool.App.Services;

public interface IShellNavCoordinator
{
    void SelectFooterNav(string navKey);

    /// <summary>与侧栏点击一致：更新操作栏、刷新目标模块并导航（如 <c>tasks</c>、<c>ideas</c>）。</summary>
    void ActivatePrimaryNav(string navKey);
}

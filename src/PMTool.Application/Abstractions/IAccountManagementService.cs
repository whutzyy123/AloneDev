namespace PMTool.Application.Abstractions;

/// <summary>本地账号目录与当前账号切换（关闭并重开 SQLite 会话）。</summary>
public interface IAccountManagementService
{
    /// <summary>切换当前账号并成功重连数据库后触发；参数为新的 <c>CurrentAccountName</c>。</summary>
    event EventHandler<string>? CurrentAccountChanged;

    Task<IReadOnlyList<string>> GetAccountsAsync(CancellationToken cancellationToken = default);
    string CurrentAccountName { get; }

    Task LoadCatalogAndApplyLastAccountAsync(CancellationToken cancellationToken = default);
    Task CreateAccountAsync(string name, CancellationToken cancellationToken = default);
    Task SwitchToAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(string name, CancellationToken cancellationToken = default);
}

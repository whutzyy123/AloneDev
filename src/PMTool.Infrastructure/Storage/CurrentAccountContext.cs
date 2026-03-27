using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Storage;

public sealed class CurrentAccountContext(IDataRootProvider dataRootProvider) : ICurrentAccountContext
{
    private string _accountName = "默认账号";

    public string CurrentAccountName => _accountName;

    public void SetCurrentAccount(string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        _accountName = accountName.Trim();
    }

    public string GetAccountDirectoryPath() =>
        Path.Combine(dataRootProvider.GetDataRootPath(), CurrentAccountName);

    public string GetDatabaseFilePath() =>
        Path.Combine(GetAccountDirectoryPath(), "pmtool.db");
}

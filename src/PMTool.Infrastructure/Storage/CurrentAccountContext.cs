using PMTool.Core.Abstractions;
using PMTool.Core.Validation;

namespace PMTool.Infrastructure.Storage;

public sealed class CurrentAccountContext(IDataRootProvider dataRootProvider) : ICurrentAccountContext
{
    private string _accountName = "默认账号";

    public string CurrentAccountName => _accountName;

    public void SetCurrentAccount(string accountName)
    {
        _accountName = AccountNameValidator.NormalizeAndValidate(accountName);
    }

    public string GetAccountDirectoryPath() =>
        Path.Combine(dataRootProvider.GetDataRootPath(), CurrentAccountName);

    public string GetDatabaseFilePath() =>
        Path.Combine(GetAccountDirectoryPath(), "pmtool.db");
}

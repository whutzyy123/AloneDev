namespace PMTool.Core.Abstractions;

/// <summary>
/// Holds the active local account name and paths under <see cref="IDataRootProvider"/>.
/// </summary>
public interface ICurrentAccountContext
{
    string CurrentAccountName { get; }

    void SetCurrentAccount(string accountName);

    /// <summary>Absolute path to <c>Data/{AccountName}</c>.</summary>
    string GetAccountDirectoryPath();

    /// <summary>Absolute path to <c>pmtool.db</c> for the current account.</summary>
    string GetDatabaseFilePath();
}

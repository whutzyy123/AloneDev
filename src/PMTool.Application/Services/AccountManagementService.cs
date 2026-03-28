using PMTool.Application.Abstractions;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;
using PMTool.Core.Validation;

namespace PMTool.Application.Services;

public sealed class AccountManagementService(
    IAccountCatalogRepository catalogRepository,
    ICurrentAccountContext accountContext,
    ISqliteConnectionHolder sqliteConnectionHolder,
    IDataRootProvider dataRootProvider) : IAccountManagementService
{
    public event EventHandler<string>? CurrentAccountChanged;

    public string CurrentAccountName => accountContext.CurrentAccountName;

    public async Task<IReadOnlyList<string>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await LoadAndSanitizeCatalogAsync(cancellationToken).ConfigureAwait(false);
        return catalog.Accounts.ToList().AsReadOnly();
    }

    public async Task LoadCatalogAndApplyLastAccountAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await LoadAndSanitizeCatalogAsync(cancellationToken).ConfigureAwait(false);
        if (catalog.Accounts.Count == 0)
        {
            catalog.Accounts.Add("默认账号");
            catalog.LastSelectedAccount = "默认账号";
            await catalogRepository.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
        }

        var last = catalog.LastSelectedAccount;
        if (string.IsNullOrWhiteSpace(last) || !catalog.Accounts.Contains(last, StringComparer.Ordinal))
        {
            last = catalog.Accounts[0];
            catalog.LastSelectedAccount = last;
            await catalogRepository.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
        }

        accountContext.SetCurrentAccount(last);
        await sqliteConnectionHolder.CloseAndReopenForCurrentAccountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateAccountAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = AccountNameValidator.NormalizeAndValidate(name);
        var catalog = await LoadAndSanitizeCatalogAsync(cancellationToken).ConfigureAwait(false);
        if (catalog.Accounts.Contains(normalized, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"账号 “{normalized}” 已存在。");
        }

        catalog.Accounts.Add(normalized);
        await catalogRepository.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
    }

    public async Task SwitchToAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = AccountNameValidator.NormalizeAndValidate(name);
        var catalog = await LoadAndSanitizeCatalogAsync(cancellationToken).ConfigureAwait(false);
        if (!catalog.Accounts.Contains(normalized, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"账号 “{normalized}” 不在目录中。");
        }

        accountContext.SetCurrentAccount(normalized);
        catalog.LastSelectedAccount = normalized;
        await catalogRepository.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
        await sqliteConnectionHolder.CloseAndReopenForCurrentAccountAsync(cancellationToken).ConfigureAwait(false);
        CurrentAccountChanged?.Invoke(this, normalized);
    }

    public async Task DeleteAccountAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = AccountNameValidator.NormalizeAndValidate(name);
        var catalog = await LoadAndSanitizeCatalogAsync(cancellationToken).ConfigureAwait(false);
        if (catalog.Accounts.Count <= 1)
        {
            throw new InvalidOperationException("至少保留一个本地账号。");
        }

        if (!catalog.Accounts.Remove(normalized))
        {
            throw new InvalidOperationException($"账号 “{normalized}” 不在目录中。");
        }

        if (string.Equals(catalog.LastSelectedAccount, normalized, StringComparison.Ordinal))
        {
            catalog.LastSelectedAccount = accountContext.CurrentAccountName;
        }

        var wasCurrent = string.Equals(accountContext.CurrentAccountName, normalized, StringComparison.Ordinal);
        if (wasCurrent)
        {
            var next = catalog.Accounts[0];
            catalog.LastSelectedAccount = next;
            await catalogRepository.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
            accountContext.SetCurrentAccount(next);
            await sqliteConnectionHolder.CloseAndReopenForCurrentAccountAsync(cancellationToken).ConfigureAwait(false);
            CurrentAccountChanged?.Invoke(this, next);
        }
        else
        {
            await catalogRepository.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
        }

        TryDeleteAccountDirectory(normalized);
    }

    private void TryDeleteAccountDirectory(string accountFolderName)
    {
        try
        {
            var root = dataRootProvider.GetDataRootPath();
            var dir = Path.Combine(root, accountFolderName);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // Catalog 已更新；目录删除失败不影响账号列表一致性。
        }
    }

    private async Task<AccountCatalog> LoadAndSanitizeCatalogAsync(CancellationToken cancellationToken)
    {
        var catalog = await catalogRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (ApplyAccountCatalogSanitization(catalog))
        {
            await catalogRepository.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
        }

        return catalog;
    }

    /// <summary>剔除篡改或损坏 JSON 中的非法账号名；修改 catalog 则返回 true。</summary>
    private static bool ApplyAccountCatalogSanitization(AccountCatalog catalog)
    {
        var dirty = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var valid = new List<string>();
        foreach (var raw in catalog.Accounts)
        {
            try
            {
                var n = AccountNameValidator.NormalizeAndValidate(raw);
                if (seen.Add(n))
                {
                    valid.Add(n);
                }
                else
                {
                    dirty = true;
                }
            }
            catch (ArgumentException)
            {
                dirty = true;
            }
        }

        if (!catalog.Accounts.SequenceEqual(valid, StringComparer.Ordinal))
        {
            dirty = true;
            catalog.Accounts.Clear();
            catalog.Accounts.AddRange(valid);
        }

        string? lastNorm = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(catalog.LastSelectedAccount))
            {
                lastNorm = AccountNameValidator.NormalizeAndValidate(catalog.LastSelectedAccount);
            }
        }
        catch (ArgumentException)
        {
            lastNorm = null;
            dirty = true;
        }

        if (catalog.Accounts.Count > 0)
        {
            if (lastNorm is null || !catalog.Accounts.Contains(lastNorm, StringComparer.Ordinal))
            {
                catalog.LastSelectedAccount = catalog.Accounts[0];
                dirty = true;
            }
            else if (!string.Equals(catalog.LastSelectedAccount, lastNorm, StringComparison.Ordinal))
            {
                catalog.LastSelectedAccount = lastNorm;
                dirty = true;
            }
        }
        else if (!string.IsNullOrWhiteSpace(catalog.LastSelectedAccount))
        {
            catalog.LastSelectedAccount = "";
            dirty = true;
        }

        return dirty;
    }
}

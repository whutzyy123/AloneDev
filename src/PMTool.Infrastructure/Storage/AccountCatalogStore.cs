using System.Text.Json;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;

namespace PMTool.Infrastructure.Storage;

/// <summary>持久化到 %LocalAppData%\AloneDev\accounts.json。</summary>
public sealed class AccountCatalogStore : IAccountCatalogRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public Task<AccountCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetCatalogPath();
        if (!File.Exists(path))
        {
            return Task.FromResult(new AccountCatalog());
        }

        var json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<AccountCatalog>(json, JsonOptions);
        return Task.FromResult(catalog ?? new AccountCatalog());
    }

    public Task SaveAsync(AccountCatalog catalog, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetCatalogPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(catalog, JsonOptions);
        File.WriteAllText(path, json);
        return Task.CompletedTask;
    }

    private static string GetCatalogPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AloneDev", "accounts.json");
}

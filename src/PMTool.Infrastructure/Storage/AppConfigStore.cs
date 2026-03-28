using System.Text.Json;
using System.Text.Json.Serialization;
using PMTool.Core.Abstractions;
using PMTool.Core.Models.DataManagement;
using PMTool.Core.Models.Settings;

namespace PMTool.Infrastructure.Storage;

public sealed class AppConfigStore(
    IDataRootProvider dataRootProvider,
    ICurrentAccountContext accountContext) : IAppConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private string ConfigPath => Path.Combine(dataRootProvider.GetDataRootPath(), "config.json");

    public async Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = ConfigPath;
        _ = Directory.CreateDirectory(dataRootProvider.GetDataRootPath());
        if (!File.Exists(path))
        {
            var fresh = AppShortcutDefaults.WithDefaultShortcuts(new AppConfiguration());
            await MergeLegacyBackupSettingsIfNeededAsync(fresh, cancellationToken).ConfigureAwait(false);
            TouchLastUpdate(fresh);
            await SaveAsync(fresh, cancellationToken).ConfigureAwait(false);
            return fresh;
        }

        try
        {
            await using var fs = File.OpenRead(path);
            var loaded = await JsonSerializer.DeserializeAsync<AppConfiguration>(fs, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var cfg = loaded ?? new AppConfiguration();
            AppShortcutDefaults.WithDefaultShortcuts(cfg);
            await MergeLegacyBackupSettingsIfNeededAsync(cfg, cancellationToken).ConfigureAwait(false);
            return cfg;
        }
        catch
        {
            var repair = AppShortcutDefaults.WithDefaultShortcuts(new AppConfiguration());
            await MergeLegacyBackupSettingsIfNeededAsync(repair, cancellationToken).ConfigureAwait(false);
            TouchLastUpdate(repair);
            await File.WriteAllTextAsync(
                    path,
                    JsonSerializer.Serialize(repair, JsonOptions),
                    cancellationToken)
                .ConfigureAwait(false);
            return repair;
        }
    }

    public async Task SaveAsync(AppConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var dir = dataRootProvider.GetDataRootPath();
        _ = Directory.CreateDirectory(dir);
        TouchLastUpdate(configuration);
        await using var fs = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(fs, configuration, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryRepairOnCorruptAsync(CancellationToken cancellationToken = default)
    {
        var path = ConfigPath;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            await using var fs = File.OpenRead(path);
            _ = await JsonSerializer.DeserializeAsync<AppConfiguration>(fs, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return false;
        }
        catch
        {
            var repair = AppShortcutDefaults.WithDefaultShortcuts(new AppConfiguration());
            await MergeLegacyBackupSettingsIfNeededAsync(repair, cancellationToken).ConfigureAwait(false);
            TouchLastUpdate(repair);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(repair, JsonOptions), cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
    }

    private async Task MergeLegacyBackupSettingsIfNeededAsync(
        AppConfiguration cfg,
        CancellationToken cancellationToken)
    {
        var legacy = Path.Combine(accountContext.GetAccountDirectoryPath(), "backup_settings.json");
        if (!File.Exists(legacy))
        {
            return;
        }

        try
        {
            await using var fs = File.OpenRead(legacy);
            var old = await JsonSerializer.DeserializeAsync<DataManagementSettings>(
                    fs,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (old is null)
            {
                return;
            }

            cfg.AutoBackup = old.AutoBackupEnabled;
            cfg.BackupRetentionCount = old.RetentionCount;
            cfg.AutoBackupMaxIntervalHours = old.MaxBackupIntervalHours;
            cfg.BackupDirectoryRelative = string.IsNullOrWhiteSpace(old.BackupDirectoryRelative)
                ? "Backup"
                : old.BackupDirectoryRelative.Trim();
            cfg.LastSuccessfulBackupUtc = old.LastSuccessfulBackupUtc;
            try
            {
                File.Delete(legacy);
            }
            catch
            {
                // ignore
            }
        }
        catch
        {
            // ignore legacy parse
        }
    }

    private static void TouchLastUpdate(AppConfiguration cfg) =>
        cfg.LastUpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
}

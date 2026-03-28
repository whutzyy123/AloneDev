using System.Text.Json;
using System.Text.Json.Serialization;
using PMTool.Core.Abstractions;
using PMTool.Core.Models.Settings;

namespace PMTool.Infrastructure.Storage;

public sealed class DataRootMigrationService(
    IDataRootProvider rootProvider,
    IConfigAnchorStore anchorStore,
    ISqliteConnectionHolder holder) : IDataRootMigrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public Task<DataRootMigrationState?> GetPendingStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = DataRootPaths.MigrationStatePath();
        if (!File.Exists(path))
        {
            return Task.FromResult<DataRootMigrationState?>(null);
        }

        try
        {
            var json = File.ReadAllText(path);
            var st = JsonSerializer.Deserialize<DataRootMigrationState>(json);
            if (st?.Phase == MigrationPhase.Completed)
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    // ignore
                }

                return Task.FromResult<DataRootMigrationState?>(null);
            }

            return Task.FromResult(st);
        }
        catch
        {
            return Task.FromResult<DataRootMigrationState?>(null);
        }
    }

    public Task ClearPendingStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var path = DataRootPaths.MigrationStatePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }

        return Task.CompletedTask;
    }

    public Task RollbackPendingOnlyAsync(CancellationToken cancellationToken = default) =>
        ClearPendingStateAsync(cancellationToken);

    public Task ValidateTargetPathAsync(string absolutePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            throw new ArgumentException("路径无效。");
        }

        var full = Path.GetFullPath(absolutePath.Trim());
        if (File.Exists(full))
        {
            throw new InvalidOperationException("请选择空文件夹作为新存储路径（当前路径是文件）。");
        }

        if (Directory.Exists(full))
        {
            if (Directory.EnumerateFileSystemEntries(full).Any())
            {
                throw new InvalidOperationException("请选择空文件夹作为新存储路径。");
            }
        }
        else
        {
            try
            {
                _ = Directory.CreateDirectory(full);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("新路径无写入权限，无法修改。", ex);
            }
        }

        return Task.CompletedTask;
    }

    public async Task RunAsync(
        string? targetRootPath,
        IProgress<(string message, int percent)>? progress,
        CancellationToken cancellationToken = default)
    {
        var pending = await GetPendingStateAsync(cancellationToken).ConfigureAwait(false);
        string source;
        string target;
        if (pending is { SourceRoot: { Length: > 0 } ps, TargetRoot: { Length: > 0 } pt })
        {
            source = Path.GetFullPath(ps.TrimEnd(Path.DirectorySeparatorChar));
            target = Path.GetFullPath(pt.TrimEnd(Path.DirectorySeparatorChar));
            if (Directory.Exists(target))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(target))
                {
                    if (File.Exists(entry))
                    {
                        File.Delete(entry);
                    }
                    else
                    {
                        Directory.Delete(entry, recursive: true);
                    }
                }
            }
            else
            {
                _ = Directory.CreateDirectory(target);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(targetRootPath))
            {
                throw new InvalidOperationException("没有待恢复的迁移，请重新选择目标路径。");
            }

            source = Path.GetFullPath(rootProvider.GetDataRootPath().TrimEnd(Path.DirectorySeparatorChar));
            target = Path.GetFullPath(targetRootPath.TrimEnd(Path.DirectorySeparatorChar));
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("新路径与当前路径相同，无需迁移。");
            }

            await ValidateTargetPathAsync(target, cancellationToken).ConfigureAwait(false);
        }

        _ = Directory.CreateDirectory(DataRootPaths.LocalAloneDevDir());
        var state = new DataRootMigrationState
        {
            SourceRoot = source,
            TargetRoot = target,
            Phase = MigrationPhase.CopyingAccounts,
            StartedAtUtc = DateTime.UtcNow,
        };
        await File.WriteAllTextAsync(
                DataRootPaths.MigrationStatePath(),
                JsonSerializer.Serialize(state, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await holder.RunExclusiveOnDatabaseFileAsync(
                    ct => CopyDataTreeAsync(source, target, progress, ct),
                    cancellationToken)
                .ConfigureAwait(false);

            state.Phase = MigrationPhase.Verifying;
            await PersistStateAsync(state, cancellationToken).ConfigureAwait(false);

            if (!Directory.GetFiles(target, "pmtool.db", SearchOption.AllDirectories).Any())
            {
                throw new InvalidOperationException("迁移校验失败：目标位置未找到数据库文件。");
            }

            state.Phase = MigrationPhase.UpdatingAnchor;
            await PersistStateAsync(state, cancellationToken).ConfigureAwait(false);

            var targetConfigPath = Path.Combine(target, "config.json");
            AppConfiguration cfg;
            if (File.Exists(targetConfigPath))
            {
                var json = await File.ReadAllTextAsync(targetConfigPath, cancellationToken).ConfigureAwait(false);
                cfg = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions) ??
                    AppShortcutDefaults.WithDefaultShortcuts(new AppConfiguration());
            }
            else
            {
                cfg = AppShortcutDefaults.WithDefaultShortcuts(new AppConfiguration());
            }

            cfg.DataPath = target;
            AppShortcutDefaults.WithDefaultShortcuts(cfg);
            await File.WriteAllTextAsync(
                    targetConfigPath,
                    JsonSerializer.Serialize(cfg, JsonOptions),
                    cancellationToken)
                .ConfigureAwait(false);

            await anchorStore.SetEffectiveDataRootAsync(target, cancellationToken).ConfigureAwait(false);
            rootProvider.SetDataRootPath(target);

            state.Phase = MigrationPhase.Completed;
            await holder.CloseAndReopenForCurrentAccountAsync(cancellationToken).ConfigureAwait(false);
            await ClearPendingStateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                if (Directory.Exists(target))
                {
                    foreach (var e in Directory.EnumerateFileSystemEntries(target))
                    {
                        if (File.Exists(e))
                        {
                            File.Delete(e);
                        }
                        else
                        {
                            Directory.Delete(e, recursive: true);
                        }
                    }
                }
            }
            catch
            {
                // best effort cleanup; anchor unchanged
            }

            await ClearPendingStateAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static Task CopyDataTreeAsync(
        string sourceDir,
        string destDir,
        IProgress<(string message, int percent)>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException("源数据目录不存在。");
        }

        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        var total = Math.Max(1, files.Length);
        for (var i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var f = files[i];
            var rel = Path.GetRelativePath(sourceDir, f);
            var destFile = Path.Combine(destDir, rel);
            var parent = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(parent))
            {
                _ = Directory.CreateDirectory(parent);
            }

            File.Copy(f, destFile, overwrite: true);
            progress?.Report(($"正在复制：{rel}", (int)((i + 1) * 100.0 / total)));
        }

        return Task.CompletedTask;
    }

    private async Task PersistStateAsync(DataRootMigrationState state, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
                DataRootPaths.MigrationStatePath(),
                JsonSerializer.Serialize(state, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

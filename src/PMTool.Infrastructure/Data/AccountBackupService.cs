using System.Globalization;
using Microsoft.Data.Sqlite;
using PMTool.Core.Abstractions;
using PMTool.Core.IO;
using PMTool.Core.Models.DataManagement;
using PMTool.Core.Models.Settings;
using PMTool.Core.Validation;

namespace PMTool.Infrastructure.Data;

public sealed class AccountBackupService(
    ICurrentAccountContext accountContext,
    ISqliteConnectionHolder holder,
    IAppConfigStore appConfigStore) : IAccountBackupService
{
    /// <summary>与 <see cref="SchemaMigration"/> 目标版本对齐；恢复时允许 ≤ 此值的库。</summary>
    private const int MaxSupportedUserVersion = 7;

    private const int MinSupportedUserVersion = 1;

    public async Task<BackupFileInfo> CreateBackupAsync(
        string? targetDirectory,
        int retentionCount,
        CancellationToken cancellationToken = default)
    {
        var cfg = await appConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var dir = targetDirectory ?? await GetBackupDirectoryAsync(cfg, accountContext, cancellationToken).ConfigureAwait(false);
        _ = Directory.CreateDirectory(dir);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var fileName = $"备份_{stamp}.db";
        var dest = Path.Combine(dir, fileName);

        await holder.RunExclusiveOnDatabaseFileAsync(
                async ct =>
                {
                    var src = accountContext.GetDatabaseFilePath();
                    File.Copy(src, dest, overwrite: false);
                    await Task.CompletedTask.ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);

        var verify = await VerifyDatabaseFileAsync(dest, cancellationToken).ConfigureAwait(false);
        if (!verify.IsOk)
        {
            try
            {
                File.Delete(dest);
            }
            catch
            {
                // ignore
            }

            throw new InvalidOperationException("备份文件损坏，请重新备份。");
        }

        if (retentionCount > 0)
        {
            TrimScheduledBackups(dir, retentionCount);
        }

        var fi = new FileInfo(dest);
        return ToBackupInfo(fi, BackupVerificationStatus.Success, null);
    }

    public async Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        var cfg = await appConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var dir = await GetBackupDirectoryAsync(cfg, accountContext, cancellationToken).ConfigureAwait(false);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        var list = new List<BackupFileInfo>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.db", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fi = new FileInfo(path);
            list.Add(ToBackupInfo(fi, BackupVerificationStatus.Unknown, null));
        }

        return list.OrderByDescending(b => b.CreatedAtUtc).ToList();
    }

    public async Task DeleteBackupAsync(string absolutePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(absolutePath))
        {
            return;
        }

        var cfg = await appConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var backupDir = await GetBackupDirectoryAsync(cfg, accountContext, cancellationToken).ConfigureAwait(false);
        var accountRoot = accountContext.GetAccountDirectoryPath();
        var full = Path.GetFullPath(absolutePath);
        var inAccount = PathSecurity.IsPathWithinDirectory(accountRoot, full);
        var inBackup = PathSecurity.IsPathWithinDirectory(backupDir, full);
        if (!inAccount && !inBackup)
        {
            throw new InvalidOperationException("只能删除当前备份目录或账户目录下的备份文件。");
        }

        File.Delete(full);
    }

    private static Task<string> GetBackupDirectoryAsync(
        AppConfiguration cfg,
        ICurrentAccountContext accountContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(cfg.AutoBackupPath))
        {
            return Task.FromResult(Path.GetFullPath(cfg.AutoBackupPath.Trim()));
        }

        var accountRoot = accountContext.GetAccountDirectoryPath();
        var rawRel = string.IsNullOrWhiteSpace(cfg.BackupDirectoryRelative)
            ? "Backup"
            : cfg.BackupDirectoryRelative.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string rel;
        try
        {
            rel = BackupDirectoryRelativeValidator.NormalizeAndValidate(rawRel);
        }
        catch (ArgumentException)
        {
            rel = "Backup";
        }

        var combined = Path.GetFullPath(Path.Combine(accountRoot, rel));
        var accountFull = Path.GetFullPath(accountRoot);
        if (!PathSecurity.IsPathWithinDirectory(accountFull, combined))
        {
            throw new InvalidOperationException(
                "备份目录配置无效（路径越出账户目录）。请在数据管理中将「备份子目录」改为合法相对路径（例如 Backup）。");
        }

        return Task.FromResult(combined);
    }

    public async Task<BackupVerificationResult> VerifyDatabaseFileAsync(
        string absolutePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(absolutePath))
        {
            return new BackupVerificationResult
            {
                IsOk = false,
                ErrorMessage = "文件不存在。",
            };
        }

        return await VerifyIntegrityOnPathAsync(absolutePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RestoreResult> RestoreFromBackupAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        var verify = await VerifyDatabaseFileAsync(backupFilePath, cancellationToken).ConfigureAwait(false);
        if (!verify.IsOk)
        {
            return new RestoreResult
            {
                Succeeded = false,
                Message = "备份文件损坏，无法恢复，请选择其他备份。",
            };
        }

        if (verify.UserVersion < MinSupportedUserVersion || verify.UserVersion > MaxSupportedUserVersion)
        {
            return new RestoreResult
            {
                Succeeded = false,
                Message = "备份文件与当前软件版本不兼容，无法恢复。",
            };
        }

        var cfg = await appConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var backupRoot = await GetBackupDirectoryAsync(cfg, accountContext, cancellationToken).ConfigureAwait(false);
        _ = Directory.CreateDirectory(backupRoot);

        var dbPath = accountContext.GetDatabaseFilePath();
        var preStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var prePath = Path.Combine(backupRoot, $"恢复前_{preStamp}.db");
        var rolledBack = false;

        try
        {
            await holder.RunExclusiveOnDatabaseFileAsync(
                    async ct =>
                    {
                        File.Copy(dbPath, prePath, overwrite: false);
                        try
                        {
                            File.Copy(backupFilePath, dbPath, overwrite: true);
                        }
                        catch
                        {
                            rolledBack = true;
                            RestoreDbFromFile(prePath, dbPath);
                            throw;
                        }

                        var post = await VerifyIntegrityOnPathAsync(dbPath, ct).ConfigureAwait(false);
                        if (!post.IsOk ||
                            post.UserVersion < MinSupportedUserVersion ||
                            post.UserVersion > MaxSupportedUserVersion)
                        {
                            rolledBack = true;
                            RestoreDbFromFile(prePath, dbPath);
                            throw new InvalidOperationException("恢复失败，已回滚至恢复前数据。");
                        }

                        await Task.CompletedTask.ConfigureAwait(false);
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return new RestoreResult
            {
                Succeeded = true,
                RolledBack = false,
                Message = "恢复成功，请重启软件。",
                PreRestoreBackupPath = prePath,
            };
        }
        catch (Exception ex)
        {
            return new RestoreResult
            {
                Succeeded = false,
                RolledBack = rolledBack,
                Message = ex.Message,
                PreRestoreBackupPath = prePath,
            };
        }
    }

    private static void RestoreDbFromFile(string prePath, string dbPath)
    {
        if (!File.Exists(prePath))
        {
            return;
        }

        File.Copy(prePath, dbPath, overwrite: true);
    }

    private static BackupFileInfo ToBackupInfo(
        FileInfo fi,
        BackupVerificationStatus status,
        string? err) =>
        new()
        {
            FileName = fi.Name,
            AbsolutePath = fi.FullName,
            CreatedAtUtc = fi.LastWriteTimeUtc,
            SizeBytes = fi.Length,
            LastVerification = status,
            VerificationError = err,
        };

    private static void TrimScheduledBackups(string backupDirectory, int retentionCount)
    {
        var candidates = Directory
            .EnumerateFiles(backupDirectory, "*.db", SearchOption.TopDirectoryOnly)
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return name.StartsWith("备份_", StringComparison.Ordinal) &&
                       !name.StartsWith("恢复前_", StringComparison.Ordinal);
            })
            .Select(f => new FileInfo(f))
            .OrderByDescending(x => x.LastWriteTimeUtc)
            .ToList();

        foreach (var fi in candidates.Skip(retentionCount))
        {
            try
            {
                fi.Delete();
            }
            catch
            {
                // ignore single file
            }
        }
    }

    private static async Task<BackupVerificationResult> VerifyIntegrityOnPathAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var csb = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
            };
            await using var conn = new SqliteConnection(csb.ConnectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA integrity_check;";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return new BackupVerificationResult { IsOk = false, ErrorMessage = "完整性检查无结果。" };
                }

                var cell = reader.GetString(0);
                if (!cell.Equals("ok", StringComparison.OrdinalIgnoreCase))
                {
                    return new BackupVerificationResult { IsOk = false, ErrorMessage = cell };
                }
            }

            int uv;
            await using (var cmd2 = conn.CreateCommand())
            {
                cmd2.CommandText = "PRAGMA user_version;";
                var scalar = await cmd2.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                uv = scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0, CultureInfo.InvariantCulture);
            }

            return new BackupVerificationResult { IsOk = true, UserVersion = uv };
        }
        catch (Exception ex)
        {
            return new BackupVerificationResult { IsOk = false, ErrorMessage = ex.Message };
        }
    }
}

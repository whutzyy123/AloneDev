using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PMTool.Core.Abstractions;
using PMTool.Core.Models.DataManagement;
using PMTool.Core.Validation;

namespace PMTool.App.ViewModels;

public partial class DataManagementViewModel(
    IAccountBackupService backupService,
    IDataExportService exportService,
    IAppConfigStore appConfigStore,
    ICurrentAccountContext accountContext) : ObservableObject
{
    [ObservableProperty]
    private string _errorBanner = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _databasePath = "";

    [ObservableProperty]
    private string _databaseSizeText = "";

    [ObservableProperty]
    private string _databaseModifiedText = "";

    [ObservableProperty]
    private bool _autoBackupEnabled;

    /// <remarks>与 WinUI NumberBox.Value（double）对齐，持久化时四舍五入为整数。</remarks>
    [ObservableProperty]
    private double _retentionCount = 7;

    [ObservableProperty]
    private double _maxBackupIntervalHours = 24;

    [ObservableProperty]
    private string _backupDirectoryRelative = "Backup";

    [ObservableProperty]
    private bool _exportProjects = true;

    [ObservableProperty]
    private bool _exportFeatures = true;

    [ObservableProperty]
    private bool _exportTasks = true;

    [ObservableProperty]
    private bool _exportReleases = true;

    [ObservableProperty]
    private bool _exportDocuments = true;

    [ObservableProperty]
    private bool _exportIdeas = true;

    /// <summary>true = CSV；false = Excel。</summary>
    [ObservableProperty]
    private bool _exportUseCsv;

    [ObservableProperty]
    private int _exportProgress;

    [ObservableProperty]
    private string _exportProgressText = "";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<DataManagementBackupRowViewModel> BackupRows { get; } = [];

    public bool ShowBackupListEmpty => BackupRows.Count == 0;

    public bool ShowBackupListHasItems => BackupRows.Count > 0;

    public bool CanExport =>
        (ExportProjects || ExportFeatures || ExportTasks || ExportReleases || ExportDocuments || ExportIdeas) &&
        !IsBusy;

    /// <summary>导出进度条可见性（与备份/恢复的 <see cref="IsBusy"/> 区分）。</summary>
    public bool ShowExportProgress =>
        !string.IsNullOrEmpty(ExportProgressText) || ExportProgress > 0;

    partial void OnExportProjectsChanged(bool value) => OnPropertyChanged(nameof(CanExport));

    partial void OnExportFeaturesChanged(bool value) => OnPropertyChanged(nameof(CanExport));

    partial void OnExportTasksChanged(bool value) => OnPropertyChanged(nameof(CanExport));

    partial void OnExportReleasesChanged(bool value) => OnPropertyChanged(nameof(CanExport));

    partial void OnExportDocumentsChanged(bool value) => OnPropertyChanged(nameof(CanExport));

    partial void OnExportIdeasChanged(bool value) => OnPropertyChanged(nameof(CanExport));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(ShowExportProgress));
    }

    partial void OnExportProgressChanged(int value) => OnPropertyChanged(nameof(ShowExportProgress));

    partial void OnExportProgressTextChanged(string value) => OnPropertyChanged(nameof(ShowExportProgress));

    [RelayCommand]
    public async Task RefreshAsync()
    {
        ErrorBanner = "";
        try
        {
            var cfg = await appConfigStore.LoadAsync().ConfigureAwait(true);
            AutoBackupEnabled = cfg.AutoBackup;
            RetentionCount = Math.Clamp(cfg.BackupRetentionCount, 1, 99);
            MaxBackupIntervalHours = Math.Clamp(cfg.AutoBackupMaxIntervalHours, 1, 8760);
            var relRaw = string.IsNullOrWhiteSpace(cfg.BackupDirectoryRelative)
                ? "Backup"
                : cfg.BackupDirectoryRelative.Trim();
            try
            {
                BackupDirectoryRelative = BackupDirectoryRelativeValidator.NormalizeAndValidate(relRaw);
            }
            catch (ArgumentException)
            {
                BackupDirectoryRelative = "Backup";
                cfg.BackupDirectoryRelative = "Backup";
                await appConfigStore.SaveAsync(cfg).ConfigureAwait(true);
                ErrorBanner = "备份子目录配置无效，已重置为 Backup。";
            }

            var db = accountContext.GetDatabaseFilePath();
            DatabasePath = db;
            if (File.Exists(db))
            {
                var fi = new FileInfo(db);
                DatabaseSizeText = FormatSizeBytes(fi.Length);
                DatabaseModifiedText = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
            }
            else
            {
                DatabaseSizeText = "—";
                DatabaseModifiedText = "—";
            }

            var list = await backupService.ListBackupsAsync().ConfigureAwait(true);
            BackupRows.Clear();
            foreach (var b in list)
            {
                BackupRows.Add(new DataManagementBackupRowViewModel
                {
                    FileName = b.FileName,
                    AbsolutePath = b.AbsolutePath,
                    SizeDisplay = FormatSizeBytes(b.SizeBytes),
                    TimeDisplay = b.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
                });
            }

            StatusMessage = BackupRows.Count == 0 ? "暂无备份文件，点击手动备份创建。" : $"共 {BackupRows.Count} 个备份文件。";
            OnPropertyChanged(nameof(ShowBackupListEmpty));
            OnPropertyChanged(nameof(ShowBackupListHasItems));
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    partial void OnAutoBackupEnabledChanged(bool value) => _ = SaveSettingsSilentlyAsync();

    partial void OnRetentionCountChanged(double value) => _ = SaveSettingsSilentlyAsync();

    partial void OnMaxBackupIntervalHoursChanged(double value) => _ = SaveSettingsSilentlyAsync();

    partial void OnBackupDirectoryRelativeChanged(string value) => _ = SaveSettingsSilentlyAsync();

    private async Task SaveSettingsSilentlyAsync()
    {
        try
        {
            var cfg = await appConfigStore.LoadAsync().ConfigureAwait(true);
            string rel;
            try
            {
                rel = BackupDirectoryRelativeValidator.NormalizeAndValidate(
                    string.IsNullOrWhiteSpace(BackupDirectoryRelative) ? "Backup" : BackupDirectoryRelative.Trim());
            }
            catch (ArgumentException ex)
            {
                ErrorBanner = ex.Message;
                rel = "Backup";
                if (!string.Equals(BackupDirectoryRelative, rel, StringComparison.Ordinal))
                {
                    BackupDirectoryRelative = rel;
                }
            }

            cfg.AutoBackup = AutoBackupEnabled;
            cfg.BackupRetentionCount = Math.Clamp((int)Math.Round(RetentionCount), 1, 99);
            cfg.AutoBackupMaxIntervalHours = Math.Clamp((int)Math.Round(MaxBackupIntervalHours), 1, 8760);
            cfg.BackupDirectoryRelative = rel;
            await appConfigStore.SaveAsync(cfg).ConfigureAwait(true);
        }
        catch
        {
            // 避免打断 UI；错误在下次 Refresh 可见
        }
    }

    public async Task CreateBackupToDirectoryAsync(string targetDirectory)
    {
        ErrorBanner = "";
        IsBusy = true;
        try
        {
            var cfg = await appConfigStore.LoadAsync().ConfigureAwait(true);
            await backupService.CreateBackupAsync(targetDirectory, cfg.BackupRetentionCount).ConfigureAwait(true);
            cfg.LastSuccessfulBackupUtc = DateTime.UtcNow;
            await appConfigStore.SaveAsync(cfg).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            StatusMessage = "备份已完成并校验通过。";
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteBackupAsync(DataManagementBackupRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        ErrorBanner = "";
        try
        {
            await backupService.DeleteBackupAsync(row.AbsolutePath).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
    }

    public async Task RestoreFromFileAsync(string backupFilePath)
    {
        ErrorBanner = "";
        IsBusy = true;
        try
        {
            var result = await backupService.RestoreFromBackupAsync(backupFilePath).ConfigureAwait(true);
            if (result.Succeeded)
            {
                StatusMessage = result.Message ?? "恢复成功。";
            }
            else
            {
                ErrorBanner = result.Message ?? "恢复失败。";
            }

            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportToFolderAsync(string? outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            ErrorBanner = "请选择导出目录。";
            return;
        }

        var modules = BuildExportModules();
        if (modules == DataExportModule.None)
        {
            ErrorBanner = "请至少勾选一个模块。";
            return;
        }

        ErrorBanner = "";
        IsBusy = true;
        ExportProgress = 0;
        ExportProgressText = "正在导出…";
        try
        {
            var request = new DataExportRequest
            {
                Modules = modules,
                Format = ExportUseCsv ? DataExportFormat.Csv : DataExportFormat.Excel,
                OutputDirectory = outputDirectory,
            };

            var progress = new Progress<(string message, int percent)>(p =>
            {
                ExportProgressText = p.message;
                ExportProgress = p.percent;
            });

            await exportService.ExportAsync(request, progress).ConfigureAwait(true);
            StatusMessage = $"导出完成：{outputDirectory}";
        }
        catch (Exception ex)
        {
            ErrorBanner = ex.Message;
        }
        finally
        {
            IsBusy = false;
            ExportProgress = 0;
            ExportProgressText = "";
        }
    }

    private DataExportModule BuildExportModules()
    {
        var m = DataExportModule.None;
        if (ExportProjects)
        {
            m |= DataExportModule.Projects;
        }

        if (ExportFeatures)
        {
            m |= DataExportModule.Features;
        }

        if (ExportTasks)
        {
            m |= DataExportModule.Tasks;
        }

        if (ExportReleases)
        {
            m |= DataExportModule.Releases;
        }

        if (ExportDocuments)
        {
            m |= DataExportModule.Documents;
        }

        if (ExportIdeas)
        {
            m |= DataExportModule.Ideas;
        }

        return m;
    }

    private static string FormatSizeBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var mb = bytes / (1024.0 * 1024.0);
        return $"{mb:F2} MB";
    }

    /// <summary>自动备份成功后更新时间戳（由调度器调用）。</summary>
    public async Task RecordSuccessfulAutoBackupAsync(CancellationToken cancellationToken = default)
    {
        var cfg = await appConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        cfg.LastSuccessfulBackupUtc = DateTime.UtcNow;
        await appConfigStore.SaveAsync(cfg, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>启动补备 / 定时器调用：在默认备份目录生成备份。</summary>
    public async Task RunScheduledBackupAsync(CancellationToken cancellationToken = default)
    {
        var cfg = await appConfigStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        await backupService.CreateBackupAsync(null, cfg.BackupRetentionCount, cancellationToken).ConfigureAwait(true);
        cfg.LastSuccessfulBackupUtc = DateTime.UtcNow;
        await appConfigStore.SaveAsync(cfg, cancellationToken).ConfigureAwait(true);
    }

    public async Task<DataManagementSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var cfg = await appConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return new DataManagementSettings
        {
            AutoBackupEnabled = cfg.AutoBackup,
            RetentionCount = cfg.BackupRetentionCount,
            MaxBackupIntervalHours = cfg.AutoBackupMaxIntervalHours,
            BackupDirectoryRelative = cfg.BackupDirectoryRelative,
            LastSuccessfulBackupUtc = cfg.LastSuccessfulBackupUtc,
        };
    }
}

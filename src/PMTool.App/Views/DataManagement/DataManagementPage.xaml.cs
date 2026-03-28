using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PMTool.App.UI;
using PMTool.App.ViewModels;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace PMTool.App.Views.DataManagement;

public sealed partial class DataManagementPage : Page
{
    public DataManagementViewModel ViewModel { get; }

    public DataManagementPage()
    {
        ViewModel = App.Services.GetRequiredService<DataManagementViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        AutoBackupSwitch.Toggled += AutoBackup_Toggled;
        Loaded += async (_, _) => await ViewModel.RefreshAsync().ConfigureAwait(true);
    }

    private void ErrorInfoBar_Closing(InfoBar sender, InfoBarClosingEventArgs args)
    {
        ViewModel.ErrorBanner = string.Empty;
    }

    private async void ManualBackup_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            ViewModel.ErrorBanner = "无法打开文件夹选择器。";
            return;
        }

        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        await ViewModel.CreateBackupToDirectoryAsync(folder.Path).ConfigureAwait(true);
    }

    private async void AutoBackup_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch { IsOn: true })
        {
            return;
        }

        var dialog = AloneDialogFactory.CreateStandard(
            XamlRoot,
            "开启自动备份",
            "将在每日凌晨 2 点（软件保持运行时）或按「最大备份间隔」在启动后补执行备份。保留数量内的早期「备份_*.db」可能被自动删除。",
            "确定");
        var r = await dialog.ShowAsync();
        if (r != ContentDialogResult.Primary)
        {
            ViewModel.AutoBackupEnabled = false;
        }
    }

    private async void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path })
        {
            return;
        }

        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        _ = await Launcher.LaunchFolderPathAsync(dir);
    }

    private async void DeleteBackupRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DataManagementBackupRowViewModel row })
        {
            return;
        }

        var dialog = AloneDialogFactory.CreateDestructiveConfirm(
            XamlRoot,
            "删除备份",
            $"确定删除「{row.FileName}」？",
            "删除");
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await ViewModel.DeleteBackupCommand.ExecuteAsync(row);
    }

    private async void PickRestoreFile_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            ViewModel.ErrorBanner = "无法打开文件选择器。";
            return;
        }

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeFilter.Add(".db");
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var confirm = AloneDialogFactory.CreateStandard(
            XamlRoot,
            "确认恢复",
            "恢复前将备份当前数据库；完成后需重启软件。是否继续？",
            "恢复");
        confirm.DefaultButton = ContentDialogButton.Primary;
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await ViewModel.RestoreFromFileAsync(file.Path).ConfigureAwait(true);
        if (string.IsNullOrEmpty(ViewModel.ErrorBanner))
        {
            var done = AloneDialogFactory.CreateInfo(XamlRoot, "恢复完成", ViewModel.StatusMessage);
            await done.ShowAsync();
        }
    }

    private async void ExportPickFolder_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            ViewModel.ErrorBanner = "无法打开文件夹选择器。";
            return;
        }

        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        await ViewModel.ExportToFolderCommand.ExecuteAsync(folder.Path);
    }
}

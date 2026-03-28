using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PMTool.App.ViewModels;
using PMTool.Core.Models.Settings;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PMTool.App.Views.Settings;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public AppThemeOption ThemeLight => AppThemeOption.Light;

    public AppThemeOption ThemeDark => AppThemeOption.Dark;

    public AppThemeOption ThemeFollowSystem => AppThemeOption.FollowSystem;

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.RefreshAsync().ConfigureAwait(true);
    }

    private async void PickMigrationFolder_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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
        if (folder is not null)
        {
            ViewModel.MigrationTargetPath = folder.Path;
        }
    }

    private async void RecordShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SettingsShortcutRowViewModel row })
        {
            return;
        }

        if (row.IsReadOnlyBinding)
        {
            return;
        }

        var result = await ShortcutRecordDialog.PickAsync(XamlRoot, row.BindingDisplay).ConfigureAwait(true);
        if (result is not null)
        {
            row.BindingDisplay = result;
        }
    }
}

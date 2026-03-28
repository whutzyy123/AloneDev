using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PMTool.App.UI;
using PMTool.App.ViewModels;
using PMTool.Core.Validation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PMTool.App.Views.Projects;

public sealed partial class ProjectListPage : Page
{
    public ProjectListViewModel ViewModel { get; }

    public ProjectListPage()
    {
        ViewModel = App.Services.GetRequiredService<ProjectListViewModel>();
        DataContext = ViewModel;
        // 在 Init 之前订阅：若 InitializeComponent 抛错，Navigate 仍会先执行到此；同时避免 Init 失败后未挂接导致命令无响应。
        ViewModel.NewProjectUiRequested += OnNewProjectUiRequested;
        ViewModel.EditProjectUiRequested += OnEditProjectUiRequested;
        try
        {
            InitializeComponent();
        }
        catch
        {
            ViewModel.NewProjectUiRequested -= OnNewProjectUiRequested;
            ViewModel.EditProjectUiRequested -= OnEditProjectUiRequested;
            throw;
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = ViewModel.RefreshAsync();
    }

    private void ErrorInfoBar_Closing(InfoBar sender, InfoBarClosingEventArgs args)
    {
        ViewModel.ErrorBanner = string.Empty;
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.NewProjectUiRequested -= OnNewProjectUiRequested;
        ViewModel.EditProjectUiRequested -= OnEditProjectUiRequested;
    }

    private async void OnNewProjectUiRequested(object? sender, EventArgs e)
    {
        try
        {
            await ShowProjectEditorDialogAsync(isEdit: false, null, null, null, null).ConfigureAwait(true);
        }
        catch
        {
        }
    }

    private async void OnEditProjectUiRequested(object? sender, EventArgs e)
    {
        if (ViewModel.SelectedProject is null)
        {
            return;
        }

        var gitRoot = await ViewModel.GetLocalGitRootForSelectedAsync().ConfigureAwait(true);
        await ShowProjectEditorDialogAsync(
            isEdit: true,
            ViewModel.SelectedProject.Name,
            ViewModel.SelectedProject.Description,
            gitRoot,
            ViewModel.SelectedProject.TechStack).ConfigureAwait(true);
    }

    private async Task ShowProjectEditorDialogAsync(
        bool isEdit,
        string? initialName,
        string? initialDesc,
        string? initialLocalGitRoot,
        string? initialTechStack)
    {
        var dialogRoot = XamlRootResolver.ForPage(this);
        if (dialogRoot is null)
        {
            return;
        }

        var nameBox = new TextBox
        {
            Header = "项目名称",
            Text = initialName ?? string.Empty,
            MaxLength = 100,
            PlaceholderText = "例如：产品迭代 A",
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(nameBox);
        var descBox = new TextBox
        {
            Header = "项目描述（可选）",
            Text = initialDesc ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MaxLength = 500,
            MinHeight = 120,
            PlaceholderText = "可选：一句话说明范围或目标",
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(descBox);

        var techBox = new TextBox
        {
            Header = "技术栈（可选）",
            Text = initialTechStack ?? string.Empty,
            MaxLength = 512,
            PlaceholderText = "例如：Vue, Rust, Flutter（逗号或分号分隔）",
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(techBox);

        var gitHint = new TextBlock
        {
            Text = "本地 Git 仓库根目录（可选）：须为含 .git 的文件夹，仅保存在本机，用于「版本」页从提交记录生成变更说明。",
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["AloneCaptionTextBlockStyle"] as Microsoft.UI.Xaml.Style,
        };
        var gitBox = new TextBox
        {
            Header = "本地 Git 仓库路径",
            Text = initialLocalGitRoot ?? string.Empty,
            PlaceholderText = @"例如 D:\Repos\MyApp",
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(gitBox);
        var browseGit = new Button
        {
            Content = "浏览文件夹…",
            Style = Microsoft.UI.Xaml.Application.Current.Resources["AloneSecondaryButtonStyle"] as Microsoft.UI.Xaml.Style,
        };
        browseGit.Click += async (_, _) =>
        {
            if (App.MainWindow is null)
            {
                ViewModel.ErrorBanner = "无法打开文件夹选择器。";
                return;
            }

            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                gitBox.Text = folder.Path;
            }
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { nameBox, descBox, techBox, gitHint, gitBox, browseGit },
        };
        var dialog = AloneDialogFactory.CreateStandard(
            dialogRoot,
            isEdit ? "编辑项目" : "新建项目",
            panel,
            "确认");

        dialog.PrimaryButtonClick += (_, args) =>
        {
            nameBox.Description = string.Empty;
            descBox.Description = string.Empty;
            techBox.Description = string.Empty;
            gitBox.Description = string.Empty;
            try
            {
                ProjectFieldValidator.ValidateName(nameBox.Text);
            }
            catch (ArgumentException aex)
            {
                args.Cancel = true;
                nameBox.Description = aex.Message;
                return;
            }

            try
            {
                ProjectFieldValidator.ValidateDescription(descBox.Text);
            }
            catch (ArgumentException aex)
            {
                args.Cancel = true;
                descBox.Description = aex.Message;
                return;
            }

            try
            {
                ProjectFieldValidator.ValidateTechStack(techBox.Text);
            }
            catch (ArgumentException aex)
            {
                args.Cancel = true;
                techBox.Description = aex.Message;
                return;
            }

            try
            {
                ProjectFieldValidator.ValidateOptionalLocalGitRoot(
                    string.IsNullOrWhiteSpace(gitBox.Text) ? null : gitBox.Text);
            }
            catch (ArgumentException aex)
            {
                args.Cancel = true;
                gitBox.Description = aex.Message;
            }
        };

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        catch
        {
            return;
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            var gitArg = string.IsNullOrWhiteSpace(gitBox.Text) ? null : gitBox.Text.Trim();
            if (isEdit && ViewModel.SelectedProject is { } sel)
            {
                await ViewModel.UpdateProjectAsync(sel.Id, nameBox.Text, descBox.Text, gitArg, techBox.Text).ConfigureAwait(true);
            }
            else
            {
                await ViewModel.CreateProjectAsync(nameBox.Text, descBox.Text, gitArg, techBox.Text).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async void DeleteProject_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedProject is not { })
        {
            return;
        }

        var root = XamlRootResolver.ForPage(this);
        if (root is null)
        {
            return;
        }

        var confirm = AloneDialogFactory.CreateDestructiveConfirm(
            root,
            "删除项目",
            "删除后不可恢复。若有关联内容将无法删除。确定删除？",
            "删除");

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ViewModel.ErrorBanner = "";
        if (ViewModel.DeleteSelectedCommand is IAsyncRelayCommand asyncDel)
        {
            await asyncDel.ExecuteAsync(default);
        }
        else
        {
            ViewModel.DeleteSelectedCommand.Execute(null);
        }
    }
}

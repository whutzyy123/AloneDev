using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using PMTool.App.Diagnostics;
using PMTool.App.UI;
using PMTool.App.ViewModels;
using PMTool.Core.Validation;

namespace PMTool.App.Views.Projects;

public sealed partial class ProjectListPage : Page
{
    public ProjectListViewModel ViewModel { get; }

    public ProjectListPage()
    {
        // #region agent log
        DebugAgentLog.Write("G", "ProjectListPage.ctor", "entered", null);
        // #endregion
        ViewModel = App.Services.GetRequiredService<ProjectListViewModel>();
        DataContext = ViewModel;
        // 在 Init 之前订阅：若 InitializeComponent 抛错，Navigate 仍会先执行到此；同时避免 Init 失败后未挂接导致命令无响应。
        ViewModel.NewProjectUiRequested += OnNewProjectUiRequested;
        ViewModel.EditProjectUiRequested += OnEditProjectUiRequested;
        // #region agent log
        DebugAgentLog.Write("F", "ProjectListPage.ctor", "subscribed before Init", null);
        // #endregion
        try
        {
            InitializeComponent();
            // #region agent log
            DebugAgentLog.Write("F", "ProjectListPage.ctor", "Init ok", null);
            // #endregion
        }
        catch (Exception ex)
        {
            ViewModel.NewProjectUiRequested -= OnNewProjectUiRequested;
            ViewModel.EditProjectUiRequested -= OnEditProjectUiRequested;
            // #region agent log
            DebugAgentLog.Write(
                "X",
                "ProjectListPage.ctor",
                "Init exception",
                new Dictionary<string, string> { ["type"] = ex.GetType().Name, ["msg"] = ex.Message });
            // #endregion
            throw;
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // #region agent log
        DebugAgentLog.Write("F", "ProjectListPage.OnLoaded", "refresh only", null);
        // #endregion
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
        // #region agent log
        DebugAgentLog.Write("A", "ProjectListPage.OnNewProjectUiRequested", "handler entered", null);
        // #endregion
        try
        {
            await ShowProjectEditorDialogAsync(isEdit: false, null, null).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // #region agent log
            DebugAgentLog.Write("C", "ProjectListPage.OnNewProjectUiRequested", "exception", new Dictionary<string, string> { ["ex"] = ex.GetType().Name, ["msg"] = ex.Message });
            // #endregion
        }
    }

    private async void OnEditProjectUiRequested(object? sender, EventArgs e)
    {
        if (ViewModel.SelectedProject is null)
        {
            return;
        }

        await ShowProjectEditorDialogAsync(
            isEdit: true,
            ViewModel.SelectedProject.Name,
            ViewModel.SelectedProject.Description).ConfigureAwait(true);
    }

    private async Task ShowProjectEditorDialogAsync(bool isEdit, string? initialName, string? initialDesc)
    {
        // #region agent log
        var dialogRoot = XamlRootResolver.ForPage(this);
        DebugAgentLog.Write(
            "B",
            "ProjectListPage.ShowProjectEditorDialogAsync:enter",
            "before CreateStandard",
            new Dictionary<string, string>
            {
                ["pageXamlRootNull"] = (XamlRoot == null).ToString(),
                ["resolvedRootNull"] = (dialogRoot == null).ToString(),
                ["isEdit"] = isEdit.ToString(),
            });
        // #endregion
        if (dialogRoot is null)
        {
            // #region agent log
            DebugAgentLog.Write("B", "ProjectListPage.ShowProjectEditorDialogAsync", "no XamlRoot, abort", null);
            // #endregion
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

        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { nameBox, descBox },
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
            }
        };

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            // #region agent log
            DebugAgentLog.Write("C", "ProjectListPage.ShowProjectEditorDialogAsync", "ShowAsync threw", new Dictionary<string, string> { ["ex"] = ex.GetType().Name, ["msg"] = ex.Message });
            // #endregion
            return;
        }

        // #region agent log
        DebugAgentLog.Write("B", "ProjectListPage.ShowProjectEditorDialogAsync", "ShowAsync returned", new Dictionary<string, string> { ["result"] = result.ToString() });
        // #endregion
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            if (isEdit && ViewModel.SelectedProject is { } sel)
            {
                await ViewModel.UpdateProjectAsync(sel.Id, nameBox.Text, descBox.Text).ConfigureAwait(true);
            }
            else
            {
                await ViewModel.CreateProjectAsync(nameBox.Text, descBox.Text).ConfigureAwait(true);
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

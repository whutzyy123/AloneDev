using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using PMTool.App.UI;
using PMTool.App.ViewModels;
using PMTool.Core;
using PMTool.Core.Models;

namespace PMTool.App.Views.Releases;

public sealed partial class ReleaseListPage : Page
{
    public ReleaseListViewModel ViewModel { get; }

    public ReleaseListPage()
    {
        ViewModel = App.Services.GetRequiredService<ReleaseListViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.NewReleaseUiRequested += OnNewReleaseUiRequested;
        ViewModel.EditReleaseUiRequested += OnEditReleaseUiRequested;
        ViewModel.AddRelationUiRequested += OnAddRelationUiRequested;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = ViewModel.RefreshAsync();
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.NewReleaseUiRequested -= OnNewReleaseUiRequested;
        ViewModel.EditReleaseUiRequested -= OnEditReleaseUiRequested;
        ViewModel.AddRelationUiRequested -= OnAddRelationUiRequested;
    }

    private async void OnNewReleaseUiRequested(object? sender, EventArgs e)
    {
        await ShowReleaseEditDialogAsync(isNew: true).ConfigureAwait(true);
    }

    private async void OnEditReleaseUiRequested(object? sender, EventArgs e)
    {
        if (ViewModel.SelectedRelease is null)
        {
            return;
        }

        var full = await ViewModel.GetReleaseEntityAsync(ViewModel.SelectedRelease.Id).ConfigureAwait(true);
        if (full is null)
        {
            return;
        }

        await ShowReleaseEditDialogAsync(isNew: false, existing: full).ConfigureAwait(true);
    }

    private async Task ShowReleaseEditDialogAsync(bool isNew, Release? existing = null)
    {
        var nameBox = new TextBox { Header = "版本名称", MaxLength = 100, Text = existing?.Name ?? string.Empty };
        AloneDialogFactory.ApplyFormTextBoxStyle(nameBox);
        var descBox = new TextBox
        {
            Header = "描述（可选）",
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MinHeight = 80,
            MaxLength = 1000,
            Text = existing?.Description ?? string.Empty,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(descBox);
        var startBox = new TextBox
        {
            Header = "开始时间（建议 yyyy-MM-dd 或 yyyy-MM-dd HH:mm）",
            Text = existing?.StartAt ?? string.Empty,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(startBox);
        var endBox = new TextBox
        {
            Header = "结束时间",
            Text = existing?.EndAt ?? string.Empty,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(endBox);
        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { nameBox, descBox, startBox, endBox },
        };
        var dialog = AloneDialogFactory.CreateStandard(
            XamlRoot,
            isNew ? "新建版本" : "编辑版本",
            panel,
            "确认");

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            if (isNew)
            {
                await ViewModel.CreateReleaseAsync(nameBox.Text, descBox.Text, startBox.Text, endBox.Text)
                    .ConfigureAwait(true);
            }
            else if (existing is not null)
            {
                await ViewModel.UpdateReleaseAsync(
                    existing.Id,
                    nameBox.Text,
                    descBox.Text,
                    startBox.Text,
                    endBox.Text).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async void OnAddRelationUiRequested(object? sender, EventArgs e)
    {
        if (ViewModel.SelectedRelease is null || string.IsNullOrEmpty(ViewModel.SelectedProjectId))
        {
            return;
        }

        var features = await ViewModel.GetProjectFeaturesForPickerAsync().ConfigureAwait(true);
        var tasks = await ViewModel.GetProjectTasksForPickerAsync().ConfigureAwait(true);

        var featList = new ListView
        {
            Header = "特性（多选）",
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 200,
        };
        foreach (var f in features)
        {
            featList.Items.Add(f);
        }

        featList.DisplayMemberPath = nameof(Feature.Name);

        var taskList = new ListView
        {
            Header = "任务（多选）",
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 200,
        };
        foreach (var t in tasks)
        {
            taskList.Items.Add(t);
        }

        taskList.DisplayMemberPath = nameof(PmTask.Name);

        var scroll = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 12,
                Padding = AloneDialogFactory.DialogContentPadding,
                Children =
                {
                    featList,
                    taskList,
                },
            },
            MaxHeight = 480,
        };

        var dialog = AloneDialogFactory.CreateStandard(XamlRoot, "添加关联", scroll, "确认");

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var pairs = new List<(string, string)>();
        foreach (var o in featList.SelectedItems.Cast<Feature>())
        {
            pairs.Add((ReleaseRelationTarget.Feature, o.Id));
        }

        foreach (var o in taskList.SelectedItems.Cast<PmTask>())
        {
            pairs.Add((ReleaseRelationTarget.Task, o.Id));
        }

        if (pairs.Count == 0)
        {
            return;
        }

        await ViewModel.AddRelationsAsync(pairs).ConfigureAwait(true);
    }

    private async void DeleteRelease_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedRelease is null)
        {
            return;
        }

        var confirm = AloneDialogFactory.CreateDestructiveConfirm(
            XamlRoot,
            "删除版本",
            "删除后不可恢复，关联记录将清除，特性与任务不受影响。确定删除？",
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

using Windows.ApplicationModel.DataTransfer;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinUiApp = Microsoft.UI.Xaml.Application;
using PMTool.App.DragDrop;
using PMTool.App.UI;
using PMTool.App.ViewModels;
using PMTool.Core;
using PMTool.Core.Models;
using PMTool.Core.Validation;

namespace PMTool.App.Views.Tasks;

public sealed partial class TaskListPage : Page
{
    public TaskListViewModel ViewModel { get; }

    private (Border Column, Brush? Default)[] _taskKanbanColumnRestore = [];

    public TaskListPage()
    {
        ViewModel = App.Services.GetRequiredService<TaskListViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.NewTaskUiRequested += OnNewTaskUiRequested;
        ViewModel.EditTaskUiRequested += OnEditTaskUiRequested;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_taskKanbanColumnRestore.Length == 0)
        {
            _taskKanbanColumnRestore =
            [
                (KanbanTaskCol0, KanbanTaskCol0.Background),
                (KanbanTaskCol1, KanbanTaskCol1.Background),
                (KanbanTaskCol2, KanbanTaskCol2.Background),
                (KanbanTaskCol3, KanbanTaskCol3.Background),
            ];
        }

        _ = ViewModel.RefreshAsync();
    }

    private void RestoreTaskKanbanColumnBackgrounds()
    {
        foreach (var (col, def) in _taskKanbanColumnRestore)
        {
            col.Background = def;
        }
    }

    private void ApplyTaskKanbanDropHighlight(Border? column)
    {
        RestoreTaskKanbanColumnBackgrounds();
        if (column is null)
        {
            return;
        }

        if (WinUiApp.Current.Resources.TryGetValue("AloneKanbanDropTargetBrush", out var o) && o is Brush hi)
        {
            column.Background = hi;
        }
    }

    private void ErrorInfoBar_Closing(InfoBar sender, InfoBarClosingEventArgs args)
    {
        ViewModel.ErrorBanner = string.Empty;
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.NewTaskUiRequested -= OnNewTaskUiRequested;
        ViewModel.EditTaskUiRequested -= OnEditTaskUiRequested;
    }

    private async void OnNewTaskUiRequested(object? sender, EventArgs e)
    {
        await ShowNewTaskDialogAsync().ConfigureAwait(true);
    }

    private async void OnEditTaskUiRequested(object? sender, EventArgs e)
    {
        if (ViewModel.SelectedTask is null)
        {
            return;
        }

        var full = await ViewModel.GetTaskEntityAsync(ViewModel.SelectedTask.Id).ConfigureAwait(true);
        if (full is null)
        {
            return;
        }

        await ShowEditTaskDialogAsync(full).ConfigureAwait(true);
    }

    private async Task ShowNewTaskDialogAsync()
    {
        var nameBox = new TextBox { Header = "任务名称", MaxLength = 100 };
        AloneDialogFactory.ApplyFormTextBoxStyle(nameBox);
        var descBox = new TextBox
        {
            Header = "描述（可选）",
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MinHeight = 80,
            MaxLength = 500,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(descBox);
        var typeCombo = new ComboBox
        {
            Header = "类型",
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
        };
        foreach (var t in TaskTypes.All)
        {
            typeCombo.Items.Add(t);
        }

        typeCombo.SelectedItem = TaskTypes.Feature;

        var sevCombo = new ComboBox
        {
            Header = "严重程度（仅 Bug）",
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            Visibility = Microsoft.UI.Xaml.Visibility.Collapsed,
        };
        foreach (var s in TaskSeverities.All)
        {
            sevCombo.Items.Add(s);
        }

        sevCombo.SelectedItem = TaskSeverities.Major;

        void SyncSeverityVisibility()
        {
            sevCombo.Visibility = typeCombo.SelectedItem as string == TaskTypes.Bug
                ? Microsoft.UI.Xaml.Visibility.Visible
                : Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        typeCombo.SelectionChanged += (_, _) => SyncSeverityVisibility();
        SyncSeverityVisibility();

        var hoursBox = new NumberBox
        {
            Header = "预估工时（小时）",
            Minimum = 0,
            Maximum = 999,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            SmallChange = 0.5,
            LargeChange = 1,
            Value = 0,
        };
        AloneDialogFactory.ApplyFormNumberBoxStyle(hoursBox);

        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { nameBox, descBox, typeCombo, sevCombo, hoursBox },
        };
        var dialog = AloneDialogFactory.CreateStandard(XamlRoot, "新建任务", panel, "确认");

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var tt = typeCombo.SelectedItem as string ?? TaskTypes.Feature;
        string? sev = tt == TaskTypes.Bug ? sevCombo.SelectedItem as string : null;

        try
        {
            _ = TaskFieldValidator.ValidateName(nameBox.Text);
            _ = TaskFieldValidator.ValidateDescription(descBox.Text);
            _ = TaskFieldValidator.ValidateEstimatedHours(hoursBox.Value);
        }
        catch (ArgumentException ex)
        {
            ViewModel.ErrorBanner = ex.Message;
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            await ViewModel.CreateTaskAsync(nameBox.Text, descBox.Text, tt, sev, hoursBox.Value).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async Task ShowEditTaskDialogAsync(PmTask full)
    {
        var nameBox = new TextBox { Header = "任务名称", Text = full.Name, MaxLength = 100 };
        AloneDialogFactory.ApplyFormTextBoxStyle(nameBox);
        var descBox = new TextBox
        {
            Header = "描述",
            Text = full.Description,
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MinHeight = 80,
            MaxLength = 500,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(descBox);
        var typeCombo = new ComboBox { Header = "类型", HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch };
        foreach (var t in TaskTypes.All)
        {
            typeCombo.Items.Add(t);
        }

        typeCombo.SelectedItem = full.TaskType;

        var sevCombo = new ComboBox
        {
            Header = "严重程度（仅 Bug）",
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
        };
        foreach (var s in TaskSeverities.All)
        {
            sevCombo.Items.Add(s);
        }

        sevCombo.SelectedItem = full.Severity ?? TaskSeverities.Major;

        void SyncSeverityVisibility()
        {
            sevCombo.Visibility = typeCombo.SelectedItem as string == TaskTypes.Bug
                ? Microsoft.UI.Xaml.Visibility.Visible
                : Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        typeCombo.SelectionChanged += (_, _) => SyncSeverityVisibility();
        SyncSeverityVisibility();

        var stCombo = new ComboBox { Header = "状态（可选非法以验证仓储拦截）", HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch };
        foreach (var s in TaskStatuses.All)
        {
            stCombo.Items.Add(s);
        }

        stCombo.SelectedItem = full.Status;

        var estBox = new NumberBox
        {
            Header = "预估工时",
            Minimum = 0,
            Maximum = 999,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            SmallChange = 0.5,
            Value = full.EstimatedHours,
        };
        AloneDialogFactory.ApplyFormNumberBoxStyle(estBox);
        var actBox = new NumberBox
        {
            Header = "实际工时",
            Minimum = 0,
            Maximum = 999,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            SmallChange = 0.5,
            Value = full.ActualHours,
        };
        AloneDialogFactory.ApplyFormNumberBoxStyle(actBox);

        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { nameBox, descBox, typeCombo, sevCombo, stCombo, estBox, actBox },
        };
        var dialog = AloneDialogFactory.CreateStandard(
            XamlRoot,
            "编辑任务",
            new ScrollViewer { Content = panel, MaxHeight = 420 },
            "保存");

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var tt = typeCombo.SelectedItem as string ?? full.TaskType;
        var st = stCombo.SelectedItem as string ?? full.Status;
        string? sev = tt == TaskTypes.Bug ? sevCombo.SelectedItem as string : null;

        try
        {
            _ = TaskFieldValidator.ValidateName(nameBox.Text);
            _ = TaskFieldValidator.ValidateDescription(descBox.Text);
            _ = TaskFieldValidator.ValidateEstimatedHours(estBox.Value);
            _ = TaskFieldValidator.ValidateActualHours(actBox.Value);
        }
        catch (ArgumentException ex)
        {
            ViewModel.ErrorBanner = ex.Message;
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            await ViewModel.UpdateTaskAsync(
                full.Id,
                nameBox.Text,
                descBox.Text,
                tt,
                sev,
                estBox.Value,
                actBox.Value,
                st).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async void DeleteTask_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedTask is null)
        {
            return;
        }

        var confirm = AloneDialogFactory.CreateDestructiveConfirm(
            XamlRoot,
            "删除任务",
            "删除后不可恢复。确定删除？",
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

    private void TaskKanbanCard_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: TaskRowViewModel row })
        {
            args.Data.Properties[KanbanDragKeys.TaskId] = row.Id;
            args.Data.Properties[KanbanDragKeys.TaskSourceStatus] = row.Status;
        }

        args.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void TaskKanbanCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskRowViewModel row })
        {
            ViewModel.SelectTaskRowCommand.Execute(row);
        }
    }

    private void TaskKanbanColumn_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.None;
        RestoreTaskKanbanColumnBackgrounds();
        if (sender is not FrameworkElement { Tag: string targetStatus })
        {
            return;
        }

        if (!e.DataView.Properties.ContainsKey(KanbanDragKeys.TaskId))
        {
            return;
        }

        string? sourceStatus = null;
        if (e.DataView.Properties.TryGetValue(KanbanDragKeys.TaskSourceStatus, out var ssObj) && ssObj is string ss)
        {
            sourceStatus = ss;
        }

        if (TaskStatusTransitions.TryValidate(sourceStatus, targetStatus, out _))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            if (sender is Border b)
            {
                ApplyTaskKanbanDropHighlight(b);
            }
        }
    }

    private async void TaskKanbanColumn_Drop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            if (sender is not FrameworkElement { Tag: string targetStatus })
            {
                return;
            }

            if (!e.DataView.Properties.TryGetValue(KanbanDragKeys.TaskId, out var idObj) || idObj is not string taskId)
            {
                return;
            }

            string? sourceStatus = null;
            if (e.DataView.Properties.TryGetValue(KanbanDragKeys.TaskSourceStatus, out var ssObj) && ssObj is string ss)
            {
                sourceStatus = ss;
            }

            if (!TaskStatusTransitions.TryValidate(sourceStatus, targetStatus, out var preErr))
            {
                ViewModel.ErrorBanner = preErr ?? "状态流转异常，变更被拒绝。";
                return;
            }

            var err = await ViewModel.MoveTaskToColumnAsync(taskId, targetStatus).ConfigureAwait(true);
            if (err is not null)
            {
                ViewModel.ErrorBanner = err;
            }
        }
        finally
        {
            RestoreTaskKanbanColumnBackgrounds();
            deferral.Complete();
        }
    }
}

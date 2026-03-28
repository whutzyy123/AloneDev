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

namespace PMTool.App.Views.Features;

public sealed partial class FeatureListPage : Page
{
    public FeatureListViewModel ViewModel { get; }

    private (Border Column, Brush? Default)[] _featureKanbanColumnRestore = [];

    public FeatureListPage()
    {
        ViewModel = App.Services.GetRequiredService<FeatureListViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.NewFeatureUiRequested += OnNewFeatureUiRequested;
        ViewModel.EditFeatureUiRequested += OnEditFeatureUiRequested;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_featureKanbanColumnRestore.Length == 0)
        {
            _featureKanbanColumnRestore =
            [
                (KanbanFeatureCol0, KanbanFeatureCol0.Background),
                (KanbanFeatureCol1, KanbanFeatureCol1.Background),
                (KanbanFeatureCol2, KanbanFeatureCol2.Background),
                (KanbanFeatureCol3, KanbanFeatureCol3.Background),
            ];
        }

        _ = ViewModel.RefreshAsync();
    }

    private void RestoreFeatureKanbanColumnBackgrounds()
    {
        foreach (var (col, def) in _featureKanbanColumnRestore)
        {
            col.Background = def;
        }
    }

    private void ApplyFeatureKanbanDropHighlight(Border? column)
    {
        RestoreFeatureKanbanColumnBackgrounds();
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
        ViewModel.NewFeatureUiRequested -= OnNewFeatureUiRequested;
        ViewModel.EditFeatureUiRequested -= OnEditFeatureUiRequested;
    }

    private async void OnNewFeatureUiRequested(object? sender, EventArgs e)
    {
        await ShowNewFeatureDialogAsync().ConfigureAwait(true);
    }

    private async void OnEditFeatureUiRequested(object? sender, EventArgs e)
    {
        if (ViewModel.SelectedFeature is null)
        {
            return;
        }

        var full = await ViewModel.GetFeatureEntityAsync(ViewModel.SelectedFeature.Id).ConfigureAwait(true);
        if (full is null)
        {
            return;
        }

        await ShowEditFeatureDialogAsync(full).ConfigureAwait(true);
    }

    private async Task ShowNewFeatureDialogAsync()
    {
        var nameBox = new TextBox { Header = "模块名称", MaxLength = 100 };
        AloneDialogFactory.ApplyFormTextBoxStyle(nameBox);
        var descBox = new TextBox
        {
            Header = "描述（可选）",
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MinHeight = 100,
            MaxLength = 2000,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(descBox);
        var priCombo = new ComboBox
        {
            Header = "优先级",
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
        };
        AloneThemeChrome.ApplyComboBoxStyle(priCombo);
        foreach (var i in new[] { 0, 1, 2, 3 })
        {
            priCombo.Items.Add(new ComboBoxItem { Content = FeaturePriorities.ToLabel(i), Tag = i });
        }

        priCombo.SelectedIndex = 2;

        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { nameBox, descBox, priCombo },
        };
        var dialog = AloneDialogFactory.CreateStandard(XamlRoot, "新建模块", panel, "确认");

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var pri = priCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is int pi
            ? pi
            : FeaturePriorities.P2;

        try
        {
            _ = FeatureFieldValidator.ValidateName(nameBox.Text);
            _ = FeatureFieldValidator.ValidateDescription(descBox.Text);
        }
        catch (ArgumentException ex)
        {
            ViewModel.ErrorBanner = ex.Message;
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            await ViewModel.CreateFeatureAsync(nameBox.Text, descBox.Text, pri).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async Task ShowEditFeatureDialogAsync(Feature full)
    {
        var nameBox = new TextBox { Header = "模块名称", Text = full.Name, MaxLength = 100 };
        AloneDialogFactory.ApplyFormTextBoxStyle(nameBox);
        var descBox = new TextBox
        {
            Header = "描述",
            Text = full.Description,
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MinHeight = 80,
            MaxLength = 2000,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(descBox);
        var priCombo = new ComboBox { Header = "优先级", HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch };
        AloneThemeChrome.ApplyComboBoxStyle(priCombo);
        foreach (var i in new[] { 0, 1, 2, 3 })
        {
            priCombo.Items.Add(new ComboBoxItem { Content = FeaturePriorities.ToLabel(i), Tag = i });
        }

        priCombo.SelectedIndex = Math.Clamp(full.Priority, 0, 3);

        var statusCombo = new ComboBox
        {
            Header = "状态（可故意选非法以验证拦截）",
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
        };
        AloneThemeChrome.ApplyComboBoxStyle(statusCombo);
        foreach (var s in FeatureStatuses.All)
        {
            statusCombo.Items.Add(s);
        }

        statusCombo.SelectedItem = full.Status;

        var acBox = new TextBox
        {
            Header = "验收标准",
            Text = full.AcceptanceCriteria,
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MinHeight = 60,
            MaxLength = 2000,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(acBox);
        var tsBox = new TextBox
        {
            Header = "技术栈",
            Text = full.TechStack,
            MaxLength = 2000,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(tsBox);
        var notesBox = new TextBox
        {
            Header = "备注",
            Text = full.Notes,
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MinHeight = 60,
            MaxLength = 2000,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(notesBox);
        var dueBox = new TextBox { Header = "截止时间（可选，yyyy-MM-dd）", Text = full.DueDate ?? string.Empty };
        AloneDialogFactory.ApplyFormTextBoxStyle(dueBox);

        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { nameBox, descBox, priCombo, statusCombo, acBox, tsBox, notesBox, dueBox },
        };
        var dialog = AloneDialogFactory.CreateStandard(
            XamlRoot,
            "编辑模块",
            new ScrollViewer { Content = panel, MaxHeight = 420 },
            "保存");

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var pri = priCombo.SelectedItem is ComboBoxItem cbi2 && cbi2.Tag is int pi2
            ? pi2
            : FeaturePriorities.Normalize(full.Priority);
        var st = statusCombo.SelectedItem as string ?? full.Status;

        try
        {
            _ = FeatureFieldValidator.ValidateName(nameBox.Text);
            _ = FeatureFieldValidator.ValidateDescription(descBox.Text);
            _ = FeatureFieldValidator.ValidateLongText(acBox.Text, "验收标准");
            _ = FeatureFieldValidator.ValidateLongText(tsBox.Text, "技术栈");
            _ = FeatureFieldValidator.ValidateLongText(notesBox.Text, "备注");
        }
        catch (ArgumentException ex)
        {
            ViewModel.ErrorBanner = ex.Message;
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            await ViewModel.UpdateFeatureAsync(
                full.Id,
                nameBox.Text,
                descBox.Text,
                pri,
                st,
                acBox.Text,
                tsBox.Text,
                notesBox.Text,
                dueBox.Text).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async void DeleteFeature_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedFeature is null)
        {
            return;
        }

        var confirm = AloneDialogFactory.CreateDestructiveConfirm(
            XamlRoot,
            "删除模块",
            "删除后不可恢复。若有关联任务将无法删除。确定删除？",
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

    private void FeatureKanbanCard_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: FeatureRowViewModel row })
        {
            args.Data.Properties[KanbanDragKeys.FeatureId] = row.Id;
            args.Data.Properties[KanbanDragKeys.FeatureSourceStatus] = row.Status;
        }

        args.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void FeatureKanbanCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FeatureRowViewModel row })
        {
            ViewModel.SelectFeatureCommand.Execute(row);
        }
    }

    private void FeatureKanbanColumn_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.None;
        RestoreFeatureKanbanColumnBackgrounds();
        if (sender is not FrameworkElement { Tag: string targetStatus })
        {
            return;
        }

        if (!e.DataView.Properties.ContainsKey(KanbanDragKeys.FeatureId))
        {
            return;
        }

        string? sourceStatus = null;
        if (e.DataView.Properties.TryGetValue(KanbanDragKeys.FeatureSourceStatus, out var ssObj) && ssObj is string ss)
        {
            sourceStatus = ss;
        }

        if (FeatureStatusTransitions.TryValidate(sourceStatus, targetStatus, out _))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            if (sender is Border b)
            {
                ApplyFeatureKanbanDropHighlight(b);
            }
        }
    }

    private async void FeatureKanbanColumn_Drop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            if (sender is not FrameworkElement { Tag: string targetStatus })
            {
                return;
            }

            if (!e.DataView.Properties.TryGetValue(KanbanDragKeys.FeatureId, out var idObj) || idObj is not string featureId)
            {
                return;
            }

            string? sourceStatus = null;
            if (e.DataView.Properties.TryGetValue(KanbanDragKeys.FeatureSourceStatus, out var ssObj) && ssObj is string ss)
            {
                sourceStatus = ss;
            }

            if (!FeatureStatusTransitions.TryValidate(sourceStatus, targetStatus, out var preErr))
            {
                ViewModel.ErrorBanner = preErr ?? "状态流转异常，变更被拒绝。";
                return;
            }

            var err = await ViewModel.MoveFeatureToColumnAsync(featureId, targetStatus).ConfigureAwait(true);
            if (err is not null)
            {
                ViewModel.ErrorBanner = err;
            }
        }
        finally
        {
            RestoreFeatureKanbanColumnBackgrounds();
            deferral.Complete();
        }
    }
}

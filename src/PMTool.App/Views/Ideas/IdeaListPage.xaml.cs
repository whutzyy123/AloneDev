using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PMTool.App.UI;
using PMTool.App.ViewModels;
using PMTool.Core;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;
using PMTool.Core.Validation;

namespace PMTool.App.Views.Ideas;

public sealed partial class IdeaListPage : Page
{
    public IdeaListViewModel ViewModel { get; }

    public IdeaListPage()
    {
        ViewModel = App.Services.GetRequiredService<IdeaListViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.NewIdeaUiRequested += OnNewIdeaUiRequested;
        ViewModel.EditIdeaUiRequested += OnEditIdeaUiRequested;
        ViewModel.AddDocumentsUiRequested += OnAddDocumentsUiRequested;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.RefreshAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.NewIdeaUiRequested -= OnNewIdeaUiRequested;
        ViewModel.EditIdeaUiRequested -= OnEditIdeaUiRequested;
        ViewModel.AddDocumentsUiRequested -= OnAddDocumentsUiRequested;
    }

    private async void OnNewIdeaUiRequested(object? sender, EventArgs e) =>
        await ShowNewIdeaDialogAsync().ConfigureAwait(true);

    private async void OnEditIdeaUiRequested(object? sender, EventArgs e)
    {
        if (ViewModel.DetailIdea is null)
        {
            return;
        }

        await ShowEditIdeaDialogAsync(ViewModel.DetailIdea).ConfigureAwait(true);
    }

    private async void OnAddDocumentsUiRequested(object? sender, EventArgs e) =>
        await ShowPickDocumentsDialogAsync().ConfigureAwait(true);

    private async Task ShowNewIdeaDialogAsync()
    {
        var titleBox = new TextBox { Header = "标题", MaxLength = 200 };
        AloneDialogFactory.ApplyFormTextBoxStyle(titleBox);
        var descBox = new TextBox
        {
            Header = "描述",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
            MaxLength = 1000,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(descBox);
        var tsBox = new TextBox { Header = "技术栈", MaxLength = 100 };
        AloneDialogFactory.ApplyFormTextBoxStyle(tsBox);
        var priCombo = new ComboBox { Header = "优先级（可选）", HorizontalAlignment = HorizontalAlignment.Stretch };
        priCombo.Items.Add(new ComboBoxItem { Content = "（无）", Tag = "" });
        foreach (var p in new[] { IdeaPriorities.P0, IdeaPriorities.P1, IdeaPriorities.P2, IdeaPriorities.P3 })
        {
            priCombo.Items.Add(new ComboBoxItem { Content = p, Tag = p });
        }

        priCombo.SelectedIndex = 0;

        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { titleBox, descBox, tsBox, priCombo },
        };
        var dialog = AloneDialogFactory.CreateStandard(XamlRoot, "新建灵感", panel, "创建");

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var priTag = priCombo.SelectedItem is ComboBoxItem pci ? pci.Tag as string : null;

        try
        {
            ViewModel.ErrorBanner = "";
            await ViewModel.CreateIdeaAsync(titleBox.Text, descBox.Text, tsBox.Text, priTag).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async Task ShowEditIdeaDialogAsync(Idea full)
    {
        var titleBox = new TextBox { Header = "标题", Text = full.Title, MaxLength = 200 };
        AloneDialogFactory.ApplyFormTextBoxStyle(titleBox);
        var descBox = new TextBox
        {
            Header = "描述",
            Text = full.Description,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
            MaxLength = 1000,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(descBox);
        var tsBox = new TextBox { Header = "技术栈", Text = full.TechStack, MaxLength = 100 };
        AloneDialogFactory.ApplyFormTextBoxStyle(tsBox);

        var statusCombo = new ComboBox { Header = "状态", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var s in IdeaStatuses.All)
        {
            statusCombo.Items.Add(s);
        }

        statusCombo.SelectedItem = full.Status;

        var priCombo = new ComboBox { Header = "优先级（可选）", HorizontalAlignment = HorizontalAlignment.Stretch };
        priCombo.Items.Add(new ComboBoxItem { Content = "（无）", Tag = "" });
        foreach (var p in new[] { IdeaPriorities.P0, IdeaPriorities.P1, IdeaPriorities.P2, IdeaPriorities.P3 })
        {
            priCombo.Items.Add(new ComboBoxItem { Content = p, Tag = p });
        }

        if (string.IsNullOrEmpty(full.Priority))
        {
            priCombo.SelectedIndex = 0;
        }
        else
        {
            for (var i = 0; i < priCombo.Items.Count; i++)
            {
                if (priCombo.Items[i] is ComboBoxItem cbi && Equals(cbi.Tag as string, full.Priority))
                {
                    priCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        var projCombo = new ComboBox
        {
            Header = "关联项目（仅已立项时有效）",
            DisplayMemberPath = "Name",
            SelectedValuePath = "Id",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = ViewModel.ProjectOptions,
        };

        if (full.Status == IdeaStatuses.Approved && !string.IsNullOrEmpty(full.LinkedProjectId))
        {
            projCombo.SelectedValue = full.LinkedProjectId;
        }
        else
        {
            projCombo.SelectedItem = null;
        }

        projCombo.IsEnabled = full.Status == IdeaStatuses.Approved;

        void SyncProjEnabled()
        {
            projCombo.IsEnabled = statusCombo.SelectedItem is string st && st == IdeaStatuses.Approved;
            if (!projCombo.IsEnabled)
            {
                projCombo.SelectedItem = null;
            }
        }

        statusCombo.SelectionChanged += (_, _) => SyncProjEnabled();

        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { titleBox, descBox, tsBox, statusCombo, priCombo, projCombo },
        };
        var dialog = AloneDialogFactory.CreateStandard(
            XamlRoot,
            "编辑灵感",
            new ScrollViewer { Content = panel, MaxHeight = 440 },
            "保存");

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var stSel = statusCombo.SelectedItem as string ?? full.Status;
        var priSel = priCombo.SelectedItem is ComboBoxItem pci ? pci.Tag as string : null;
        var projId = projCombo.SelectedValue as string;

        try
        {
            ViewModel.ErrorBanner = "";
            await ViewModel.UpdateIdeaFromDialogAsync(
                    full.Id,
                    titleBox.Text,
                    descBox.Text,
                    tsBox.Text,
                    stSel,
                    priSel,
                    projId)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async Task ShowPickDocumentsDialogAsync()
    {
        if (ViewModel.SelectedIdeaRow is null)
        {
            return;
        }

        var docRepo = App.Services.GetRequiredService<IDocumentRepository>();
        IReadOnlyList<PmDocument> active;
        try
        {
            active = await docRepo.ListActiveAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
            return;
        }

        var linkedIds = ViewModel.DetailDocLinks.Select(l => l.DocumentId).ToHashSet(StringComparer.Ordinal);
        var choices = active.Where(d => !linkedIds.Contains(d.Id)).ToList();
        if (choices.Count == 0)
        {
            ViewModel.ErrorBanner = "没有可关联的文档（均已关联或暂无未删除文档）。";
            return;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 360,
            ItemsSource = choices,
            DisplayMemberPath = "Name",
        };

        var listHost = new StackPanel
        {
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { list },
        };
        var dialog = AloneDialogFactory.CreateStandard(XamlRoot, "选择要关联的文档", listHost, "关联所选");

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var ids = list.SelectedItems.OfType<PmDocument>().Select(d => d.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            await ViewModel.AddDocumentLinksAsync(ids).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async void DeleteIdea_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.DetailIdea is null)
        {
            return;
        }

        var dialog = AloneDialogFactory.CreateDestructiveConfirm(
            XamlRoot,
            "删除灵感",
            "将软删除该灵感及其文档关联行，文档本体保留。是否继续？",
            "删除");

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            await ViewModel.DeleteSelectedAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }
}

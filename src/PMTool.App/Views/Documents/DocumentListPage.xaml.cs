using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PMTool.App.UI;
using PMTool.App.ViewModels;
using PMTool.Core;
using PMTool.Core.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PMTool.App.Views.Documents;

public sealed partial class DocumentListPage : Page
{
    public DocumentListViewModel ViewModel { get; }

    public DocumentListPage()
    {
        ViewModel = App.Services.GetRequiredService<DocumentListViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.NewDocumentUiRequested += OnNewDocumentUiRequested;
        ViewModel.ExportHtmlUiRequested += OnExportHtmlUiRequested;
        ViewModel.CaretMoveRequested += OnCaretMoveRequested;
        DocList.ContainerContentChanging += DocList_ContainerContentChanging;
        MarkdownEditor.Paste += MarkdownEditor_Paste;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.EnsureAutosaveTimer();
        _ = ViewModel.RefreshAsync();
    }

    private async void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.NewDocumentUiRequested -= OnNewDocumentUiRequested;
        ViewModel.ExportHtmlUiRequested -= OnExportHtmlUiRequested;
        ViewModel.CaretMoveRequested -= OnCaretMoveRequested;
        DocList.ContainerContentChanging -= DocList_ContainerContentChanging;
        MarkdownEditor.Paste -= MarkdownEditor_Paste;
        await ViewModel.FlushPendingDebouncedAutosaveAsync().ConfigureAwait(true);
        ViewModel.ReleaseDebouncedAutosaveTimer();
    }

    private void OnCaretMoveRequested(object? sender, int index)
    {
        MarkdownEditor.SelectionStart = index;
        MarkdownEditor.SelectionLength = 0;
    }

    private async void OnNewDocumentUiRequested(object? sender, EventArgs e)
    {
        await ShowNewDocumentDialogAsync().ConfigureAwait(true);
    }

    private async void OnExportHtmlUiRequested(object? sender, EventArgs e)
    {
        try
        {
            ViewModel.ErrorBanner = "";
            if (App.MainWindow is null)
            {
                ViewModel.ErrorBanner = "无法打开保存对话框（主窗口未就绪）。";
                return;
            }

            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            picker.SuggestedFileName = SanitizeFileName(ViewModel.EditorName);
            picker.FileTypeChoices.Add("HTML", [".html"]);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            if (ViewModel.HasUnsavedChanges)
            {
                await ViewModel.SaveCurrentCommand.ExecuteAsync(null).ConfigureAwait(true);
                if (ViewModel.HasUnsavedChanges)
                {
                    return;
                }
            }

            await ViewModel.ExportHtmlToPathAsync(file.Path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "document" : s[..Math.Min(s.Length, 80)];
    }

    private async Task ShowNewDocumentDialogAsync()
    {
        var typeRb = new RadioButtons { Header = "关联" };
        typeRb.Items.Add("全局文档");
        typeRb.Items.Add("项目");
        typeRb.Items.Add(DocumentRelateTypes.Feature);
        typeRb.SelectedIndex = 0;

        var projectCb = new ComboBox
        {
            Header = "项目",
            DisplayMemberPath = "Name",
            SelectedValuePath = "Id",
            ItemsSource = ViewModel.DialogProjectsMutable,
            Margin = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 0),
            Visibility = Microsoft.UI.Xaml.Visibility.Collapsed,
        };
        AloneThemeChrome.ApplyComboBoxStyle(projectCb);

        var featureCb = new ComboBox
        {
            Header = DocumentRelateTypes.Feature,
            DisplayMemberPath = "Name",
            SelectedValuePath = "Id",
            Margin = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 0),
            Visibility = Microsoft.UI.Xaml.Visibility.Collapsed,
        };
        AloneThemeChrome.ApplyComboBoxStyle(featureCb);

        var nameBox = new TextBox { Header = "文档名称", Margin = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 0), MaxLength = 100 };
        AloneDialogFactory.ApplyFormTextBoxStyle(nameBox);

        async Task LoadFeaturesAsync()
        {
            featureCb.Items.Clear();
            if (projectCb.SelectedValue is not string pid || string.IsNullOrEmpty(pid))
            {
                return;
            }

            var list = await ViewModel.LoadFeaturesForProjectAsync(pid).ConfigureAwait(true);
            foreach (var f in list)
            {
                featureCb.Items.Add(f);
            }
        }

        void SyncVisibility()
        {
            var i = typeRb.SelectedIndex;
            projectCb.Visibility = i >= 1 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            featureCb.Visibility = i >= 2 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        typeRb.SelectionChanged += (_, _) => SyncVisibility();
        projectCb.SelectionChanged += async (_, _) => await LoadFeaturesAsync().ConfigureAwait(true);

        var panel = new StackPanel
        {
            Spacing = 4,
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { typeRb, projectCb, featureCb, nameBox },
        };
        SyncVisibility();

        var dialog = AloneDialogFactory.CreateStandard(XamlRoot, "新建文档", panel, "创建");

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            var nm = nameBox.Text;
            switch (typeRb.SelectedIndex)
            {
                case 0:
                    await ViewModel.CreateDocumentAsync(DocumentRelateTypes.Global, null, null, nm).ConfigureAwait(true);
                    break;
                case 1:
                    if (projectCb.SelectedValue is not string p1 || string.IsNullOrEmpty(p1))
                    {
                        ViewModel.ErrorBanner = "请选择项目。";
                        return;
                    }

                    await ViewModel.CreateDocumentAsync(DocumentRelateTypes.Project, p1, null, nm).ConfigureAwait(true);
                    break;
                default:
                    if (projectCb.SelectedValue is not string p2 || string.IsNullOrEmpty(p2))
                    {
                        ViewModel.ErrorBanner = "请选择项目。";
                        return;
                    }

                    if (featureCb.SelectedValue is not string fid || string.IsNullOrEmpty(fid))
                    {
                        ViewModel.ErrorBanner = "请选择模块。";
                        return;
                    }

                    await ViewModel.CreateDocumentAsync(DocumentRelateTypes.Feature, p2, fid, nm).ConfigureAwait(true);
                    break;
            }
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async void RenameDocument_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.CurrentLoadedDocumentId is null)
        {
            return;
        }

        var box = new TextBox
        {
            Header = "新名称",
            MaxLength = 100,
            Text = ViewModel.EditorName,
        };
        AloneDialogFactory.ApplyFormTextBoxStyle(box);
        var wrapped = new StackPanel
        {
            Padding = AloneDialogFactory.DialogContentPadding,
            Children = { box },
        };
        var dialog = AloneDialogFactory.CreateStandard(XamlRoot, "重命名文档", wrapped, "保存");

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            await ViewModel.RenameLoadedDocumentAsync(box.Text).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private async void DeleteDocument_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.CurrentLoadedDocumentId is null)
        {
            return;
        }

        var confirm = AloneDialogFactory.CreateDestructiveConfirm(
            XamlRoot,
            "删除文档",
            "确定删除此文档？关联的图片文件将尝试一并删除。",
            "删除");

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            ViewModel.ErrorBanner = "";
            await ViewModel.DeleteLoadedDocumentAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorBanner = ex.Message;
        }
    }

    private void DocList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is ListViewItem item && args.Item is DocumentListRowViewModel row)
        {
            item.IsEnabled = !row.IsSectionHeader;
        }
    }

    private async void MarkdownEditor_Paste(object sender, TextControlPasteEventArgs e)
    {
        var data = Clipboard.GetContent();
        if (!data.Contains(StandardDataFormats.Bitmap))
        {
            return;
        }

        e.Handled = true;
        await ViewModel.HandlePasteImageAsync(MarkdownEditor.SelectionStart).ConfigureAwait(true);
    }
}

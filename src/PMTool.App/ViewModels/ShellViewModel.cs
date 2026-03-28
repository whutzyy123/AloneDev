using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using PMTool.App.Services;
using PMTool.App.Views.DataManagement;
using PMTool.App.Views.Documents;
using PMTool.App.Views.Features;
using PMTool.App.Views.Ideas;
using PMTool.App.Views.Settings;
using PMTool.App.Views.Projects;
using PMTool.App.Views.Releases;
using PMTool.App.Views.Snippets;
using PMTool.App.Views.Tasks;

namespace PMTool.App.ViewModels;

public partial class ShellViewModel(
    INavigationService navigationService,
    ProjectListViewModel projectListViewModel,
    FeatureListViewModel featureListViewModel,
    TaskListViewModel taskListViewModel,
    ReleaseListViewModel releaseListViewModel,
    DocumentListViewModel documentListViewModel,
    SnippetListViewModel snippetListViewModel,
    IdeaListViewModel ideaListViewModel,
    DataManagementViewModel dataManagementViewModel,
    DisabledOperationBarViewModel disabledOperationBarViewModel,
    Func<SettingsViewModel> getSettingsViewModel) : ObservableObject
{
    public ProjectListViewModel ProjectList => projectListViewModel;

    public FeatureListViewModel FeatureList => featureListViewModel;

    public TaskListViewModel TaskList => taskListViewModel;

    public ReleaseListViewModel ReleaseList => releaseListViewModel;

    public DocumentListViewModel DocumentList => documentListViewModel;

    public SnippetListViewModel SnippetList => snippetListViewModel;

    public IdeaListViewModel IdeaList => ideaListViewModel;

    public DataManagementViewModel DataManagement => dataManagementViewModel;

    [ObservableProperty]
    private IOperationBarViewModel _currentOperationBar = projectListViewModel;

    [ObservableProperty]
    private string _moduleTitle = "项目";

    [ObservableProperty]
    private string _activeNavKey = "projects";

    public ObservableCollection<NavEntryViewModel> PrimaryNavEntries { get; } =
    [
        new() { Key = "projects", Label = "项目", Glyph = Symbol.Home },
        new() { Key = "features", Label = "模块", Glyph = Symbol.Library },
        new() { Key = "tasks", Label = "任务", Glyph = Symbol.Bullets },
        new() { Key = "releases", Label = "版本", Glyph = Symbol.Sync },
        new() { Key = "documents", Label = "文档", Glyph = Symbol.Page2 },
        new() { Key = "snippets", Label = "代码片段", Glyph = Symbol.Copy },
        new() { Key = "ideas", Label = "灵感池", Glyph = Symbol.OutlineStar },
    ];

    public ObservableCollection<NavEntryViewModel> FooterNavEntries { get; } =
    [
        new() { Key = "data", Label = "数据管理", Glyph = Symbol.SaveLocal, IconOnly = true },
        new() { Key = "settings", Label = "系统设置", Glyph = Symbol.Setting, IconOnly = true },
    ];

    [RelayCommand]
    private void SelectNav(NavEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        SetActive(entry);
        ModuleTitle = entry.Label;
        ActiveNavKey = entry.Key;

        switch (entry.Key)
        {
            case "projects":
                CurrentOperationBar = projectListViewModel;
                _ = ProjectList.RefreshAsync();
                navigationService.NavigateTo(typeof(ProjectListPage));
                break;
            case "features":
                CurrentOperationBar = featureListViewModel;
                _ = FeatureList.RefreshAsync();
                navigationService.NavigateTo(typeof(FeatureListPage));
                break;
            case "tasks":
                CurrentOperationBar = taskListViewModel;
                _ = TaskList.RefreshAsync();
                navigationService.NavigateTo(typeof(TaskListPage));
                break;
            case "releases":
                CurrentOperationBar = releaseListViewModel;
                _ = ReleaseList.RefreshAsync();
                navigationService.NavigateTo(typeof(ReleaseListPage));
                break;
            case "documents":
                CurrentOperationBar = documentListViewModel;
                _ = DocumentList.RefreshAsync();
                navigationService.NavigateTo(typeof(DocumentListPage));
                break;
            case "snippets":
                CurrentOperationBar = snippetListViewModel;
                _ = SnippetList.RefreshAsync();
                navigationService.NavigateTo(typeof(SnippetListPage));
                break;
            case "ideas":
                CurrentOperationBar = ideaListViewModel;
                _ = IdeaList.RefreshAsync();
                navigationService.NavigateTo(typeof(IdeaListPage));
                break;
            case "data":
                CurrentOperationBar = disabledOperationBarViewModel;
                _ = dataManagementViewModel.RefreshAsync();
                if (!navigationService.NavigateTo(typeof(DataManagementPage)))
                {
                    dataManagementViewModel.ErrorBanner = "无法打开数据管理页，请重试或重启应用。";
                }

                break;
            case "settings":
                CurrentOperationBar = disabledOperationBarViewModel;
                _ = getSettingsViewModel().RefreshAsync();
                navigationService.NavigateTo(typeof(SettingsPage));
                break;
        }
    }

    /// <summary>由设置页等跳转到底栏模块（避免循环依赖时用 <see cref="IShellNavCoordinator"/>）。</summary>
    public void SelectFooterNav(string navKey)
    {
        var entry = FooterNavEntries.FirstOrDefault(e => string.Equals(e.Key, navKey, StringComparison.Ordinal));
        if (entry is not null)
        {
            SelectNav(entry);
        }
    }

    public void ActivateDefaultNav()
    {
        var first = PrimaryNavEntries[0];
        SelectNav(first);
    }

    /// <summary>全局搜索等场景：按 <see cref="NavEntryViewModel.Key"/> 切换主导航。</summary>
    public void NavigateToPrimaryModule(string navKey)
    {
        var entry = PrimaryNavEntries.FirstOrDefault(e => e.Key == navKey);
        if (entry is not null)
        {
            SelectNav(entry);
        }
    }

    private void SetActive(NavEntryViewModel selected)
    {
        foreach (var e in PrimaryNavEntries)
        {
            e.IsActive = ReferenceEquals(e, selected);
        }

        foreach (var e in FooterNavEntries)
        {
            e.IsActive = ReferenceEquals(e, selected);
        }
    }
}

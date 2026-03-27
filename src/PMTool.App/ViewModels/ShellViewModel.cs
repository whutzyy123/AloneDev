using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using PMTool.App.Services;
using PMTool.App.Views.Placeholder;
using PMTool.App.Views.Projects;

namespace PMTool.App.ViewModels;

public partial class ShellViewModel(INavigationService navigationService) : ObservableObject
{
    [ObservableProperty]
    private string _moduleTitle = "项目";

    public ObservableCollection<NavEntryViewModel> PrimaryNavEntries { get; } =
    [
        new() { Key = "projects", Label = "项目", Glyph = Symbol.Home },
        new() { Key = "features", Label = "特性", Glyph = Symbol.Library },
        new() { Key = "tasks", Label = "任务", Glyph = Symbol.Bullets },
        new() { Key = "releases", Label = "版本", Glyph = Symbol.Sync },
        new() { Key = "documents", Label = "文档", Glyph = Symbol.Page2 },
        new() { Key = "ideas", Label = "灵感池", Glyph = Symbol.OutlineStar },
    ];

    public ObservableCollection<NavEntryViewModel> FooterNavEntries { get; } =
    [
        new() { Key = "data", Label = "数据管理", Glyph = Symbol.SaveLocal },
        new() { Key = "settings", Label = "系统设置", Glyph = Symbol.Setting },
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

        switch (entry.Key)
        {
            case "projects":
                navigationService.NavigateTo(typeof(ProjectListPage));
                break;
            case "features":
                navigationService.NavigateTo(typeof(ModulePlaceholderPage), "特性");
                break;
            case "tasks":
                navigationService.NavigateTo(typeof(ModulePlaceholderPage), "任务");
                break;
            case "releases":
                navigationService.NavigateTo(typeof(ModulePlaceholderPage), "版本");
                break;
            case "documents":
                navigationService.NavigateTo(typeof(ModulePlaceholderPage), "文档");
                break;
            case "ideas":
                navigationService.NavigateTo(typeof(ModulePlaceholderPage), "灵感池");
                break;
            case "data":
                navigationService.NavigateTo(typeof(ModulePlaceholderPage), "数据管理");
                break;
            case "settings":
                navigationService.NavigateTo(typeof(ModulePlaceholderPage), "系统设置");
                break;
        }
    }

    public void ActivateDefaultNav()
    {
        var first = PrimaryNavEntries[0];
        SelectNav(first);
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

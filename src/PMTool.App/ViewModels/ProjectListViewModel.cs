using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;

namespace PMTool.App.ViewModels;

public sealed partial class ProjectListViewModel : ObservableObject
{
    public ProjectListViewModel()
    {
        Projects =
        [
            new ProjectRowViewModel
            {
                Id = "1",
                Name = "AloneDev 客户端",
                Status = "进行中",
                FeatureCount = 6,
                TaskCount = 18,
                ReleaseCount = 2,
                Description = "WinUI 3 单机项目跟进工具（占位文案）。",
            },
            new ProjectRowViewModel
            {
                Id = "2",
                Name = "个人知识库",
                Status = "进行中",
                FeatureCount = 3,
                TaskCount = 9,
                ReleaseCount = 1,
                Description = "文档与代码片段归档。",
            },
            new ProjectRowViewModel
            {
                Id = "3",
                Name = "Side Project · 记账",
                Status = "已归档",
                FeatureCount = 0,
                TaskCount = 0,
                ReleaseCount = 0,
                Description = "已搁置示例条目。",
            },
        ];
    }

    public ObservableCollection<ProjectRowViewModel> Projects { get; }

    [ObservableProperty]
    private ProjectRowViewModel? _selectedProject;

    public Visibility DetailPanelVisibility =>
        SelectedProject is null ? Visibility.Collapsed : Visibility.Visible;

    public string DetailTitle => SelectedProject?.Name ?? string.Empty;

    public string DetailStatus => SelectedProject?.Status ?? string.Empty;

    public string DetailBody => SelectedProject?.Description ?? string.Empty;

    public string DetailStats => SelectedProject?.SummaryLine ?? string.Empty;

    partial void OnSelectedProjectChanged(ProjectRowViewModel? value)
    {
        OnPropertyChanged(nameof(DetailPanelVisibility));
        OnPropertyChanged(nameof(DetailTitle));
        OnPropertyChanged(nameof(DetailStatus));
        OnPropertyChanged(nameof(DetailBody));
        OnPropertyChanged(nameof(DetailStats));
    }

    [RelayCommand]
    private void SelectProject(ProjectRowViewModel? row)
    {
        SelectedProject = row;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedProject = null;
    }
}

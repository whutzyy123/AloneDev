using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using PMTool.App.ViewModels;

namespace PMTool.App.Views.Projects;

public sealed partial class ProjectListPage : Page
{
    public ProjectListViewModel ViewModel { get; }

    public ProjectListPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ProjectListViewModel>();
        DataContext = ViewModel;
    }
}

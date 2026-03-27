using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PMTool.App.Services;
using PMTool.App.ViewModels;

namespace PMTool.App.Views.Shell;

public sealed partial class MainShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    public MainShellPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        DataContext = ViewModel;
        var nav = App.Services.GetRequiredService<INavigationService>();
        nav.ContentFrame = ContentFrame;
        Loaded += OnLoaded;
        OpenSearchButton.Click += OpenSearchButton_Click;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.ActivateDefaultNav();
    }

    private void OpenSearchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.Services.GetRequiredService<GlobalSearchUiCoordinator>().TryOpen(OpenSearchButton);
    }
}

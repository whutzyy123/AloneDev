using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace PMTool.App.Views.Placeholder;

public sealed partial class ModulePlaceholderPage : Page
{
    public ModulePlaceholderViewModel ViewModel { get; }

    public ModulePlaceholderPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ModulePlaceholderViewModel>();
        DataContext = ViewModel;
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string title)
        {
            ViewModel.ModuleTitle = title;
        }
    }
}

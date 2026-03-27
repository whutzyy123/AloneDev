using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace PMTool.App.Controls;

public sealed partial class GlobalSearchPanel : UserControl
{
    private readonly DispatcherQueueTimer _debounce;

    public GlobalSearchPanel()
    {
        InitializeComponent();
        var queue = DispatcherQueue.GetForCurrentThread();
        _debounce = queue.CreateTimer();
        _debounce.Interval = TimeSpan.FromMilliseconds(500);
        _debounce.IsRepeating = false;
        _debounce.Tick += (_, _) =>
        {
            // Future: raise event / IGlobalSearchService.Search(QueryBox.Text)
        };
        QueryBox.TextChanged += (_, _) =>
        {
            _debounce.Stop();
            _debounce.Start();
        };
    }
}

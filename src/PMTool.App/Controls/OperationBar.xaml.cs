using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using PMTool.App.Diagnostics;

namespace PMTool.App.Controls;

public sealed partial class OperationBar : UserControl
{
    public OperationBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ModuleSearchBox.QueryIcon = new SymbolIcon { Symbol = Symbol.Find };
        // #region agent log
        var ctx = DataContext?.GetType().FullName ?? "null";
        DebugAgentLog.Write("E", "OperationBar.Loaded", "DataContext type", new Dictionary<string, string> { ["dataContext"] = ctx });
        // #endregion
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(OperationBar),
        new PropertyMetadata(string.Empty));
}

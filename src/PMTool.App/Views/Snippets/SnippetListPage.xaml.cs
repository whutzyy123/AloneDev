using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using PMTool.App.ViewModels;
using Windows.UI;

namespace PMTool.App.Views.Snippets;

public sealed partial class SnippetListPage : Page
{
    private bool _previewReady;

    public SnippetListViewModel ViewModel { get; }

    public SnippetListPage()
    {
        ViewModel = App.Services.GetRequiredService<SnippetListViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += (_, _) =>
        {
            if (_previewReady)
            {
                ApplyPreviewWebHostColor();
                RefreshPreviewHtml();
            }
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.RefreshAsync();
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.SnippetPreviewInvalidated += OnPreviewInvalidated;
        try
        {
            await PreviewWeb.EnsureCoreWebView2Async();
            PreviewWeb.NavigationStarting += PreviewWeb_NavigationStarting;
            _previewReady = true;
            ApplyPreviewWebHostColor();
            RefreshPreviewHtml();
        }
        catch
        {
            _previewReady = false;
        }
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.SnippetPreviewInvalidated -= OnPreviewInvalidated;
        if (_previewReady)
        {
            PreviewWeb.NavigationStarting -= PreviewWeb_NavigationStarting;
        }

        _previewReady = false;
    }

    private void PreviewWeb_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        var uri = args.Uri ?? "";
        if (uri.Length == 0 || uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        args.Cancel = true;
    }

    private void OnPreviewInvalidated(object? sender, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshPreviewHtml();
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(RefreshPreviewHtml);
        }
    }

    private void RefreshPreviewHtml()
    {
        if (!_previewReady || PreviewWeb.CoreWebView2 is null)
        {
            return;
        }

        var lang = string.IsNullOrWhiteSpace(ViewModel.EditorLanguage) ? "plaintext" : ViewModel.EditorLanguage.Trim();
        var baseDir = AppContext.BaseDirectory;
        var css = Path.Combine(baseDir, "Assets", "CodeHighlight", "atom-one-dark.min.css");
        var js = Path.Combine(baseDir, "Assets", "CodeHighlight", "highlight.min.js");
        if (!File.Exists(css) || !File.Exists(js))
        {
            return;
        }

        var cssUri = new Uri(css).AbsoluteUri;
        var jsUri = new Uri(js).AbsoluteUri;
        var src = ViewModel.SourceText ?? "";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(src));
        var langEsc = System.Net.WebUtility.HtmlEncode(lang);

        var html =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><link rel=\"stylesheet\" href=\"" + cssUri + "\">" +
            "<style>html,body{margin:0;padding:0;height:100%;background:#282c34;color:#abb2bf;}" +
            "body{padding:12px;box-sizing:border-box;}" +
            "pre{margin:0;white-space:pre-wrap;word-break:break-word;}" +
            "code{font-family:Cascadia Code,Consolas,Courier New,monospace;font-size:13px;}</style></head><body>" +
            "<pre><code class=\"language-" + langEsc + "\" id=\"c\"></code></pre>" +
            "<script src=\"" + jsUri + "\"></script><script>" +
            "try{var s=atob(\"" + b64 + "\");document.getElementById(\"c\").textContent=s;}" +
            "catch(e){document.getElementById(\"c\").textContent=\"\";}" +
            "if(window.hljs){hljs.highlightAll();}" +
            "</script></body></html>";

        PreviewWeb.NavigateToString(html);
    }

    private void ApplyPreviewWebHostColor()
    {
        if (!_previewReady)
        {
            return;
        }

        var dark = ActualTheme switch
        {
            ElementTheme.Dark => true,
            ElementTheme.Light => false,
            _ => Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark,
        };
        PreviewWeb.DefaultBackgroundColor = dark
            ? Color.FromArgb(255, 0x35, 0x3B, 0x45)
            : Color.FromArgb(255, 0x28, 0x2C, 0x34);
    }
}

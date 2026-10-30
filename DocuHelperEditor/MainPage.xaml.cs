using Microsoft.Maui.Controls;
#if WINDOWS
using DocuHelperEditor.WinUI;
#endif

namespace DocuHelperEditor
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
#if WINDOWS
            blazorWebView.BlazorWebViewInitialized += (sender, e) =>
            {
#if !DEBUG
                e.WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#endif
                WebViewFileBridge.Attach(e.WebView.CoreWebView2);
            };
#endif
        }
    }
}

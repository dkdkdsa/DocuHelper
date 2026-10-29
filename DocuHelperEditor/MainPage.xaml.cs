using Microsoft.Maui.Controls;

namespace DocuHelperEditor
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
#if WINDOWS && !DEBUG
            blazorWebView.BlazorWebViewInitialized += (sender, e) =>
                e.WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#endif
        }
    }
}

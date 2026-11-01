using Microsoft.Web.WebView2.Core;

namespace DocuHelperEditor.WinUI
{
    public static class WebViewHost
    {
        public static CoreWebView2 Current { get; private set; }

        public static void Attach(CoreWebView2 webView)
        {
            Current = webView;
        }
    }
}

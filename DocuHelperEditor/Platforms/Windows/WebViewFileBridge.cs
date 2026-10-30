using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using DocuHelperEditor.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Web.WebView2.Core;

namespace DocuHelperEditor.WinUI
{
    public static class WebViewFileBridge
    {
        private const string FilesMessage = "docuhelper:files";

        public static void Attach(CoreWebView2 webView)
        {
            webView.WebMessageReceived += OnWebMessageReceived;
        }

        private static void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message;
            try
            {
                message = e.TryGetWebMessageAsString();
            }
            catch (Exception)
            {
                return;
            }

            if (message != FilesMessage)
                return;

            var paths = new List<string>();
            var objects = e.AdditionalObjects;
            if (objects != null)
            {
                foreach (var item in objects)
                {
                    if (item is CoreWebView2File file && !string.IsNullOrEmpty(file.Path))
                        paths.Add(file.Path);
                }
            }

            var workspace = IPlatformApplication.Current?.Services.GetService<WorkspaceStore>();
            if (workspace != null && paths.Count > 0)
                _ = ImportAsync(workspace, paths);
        }

        private static async Task ImportAsync(WorkspaceStore workspace, List<string> paths)
        {
            try
            {
                await workspace.ImportDroppedAsync(paths);
            }
            catch (Exception e)
            {
                AppLog.Write("Dropped files failed", e);
            }
        }
    }
}

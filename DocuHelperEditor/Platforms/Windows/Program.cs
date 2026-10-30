using System;
using System.Threading;
using DocuHelperEditor.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;
using WinRT;

namespace DocuHelperEditor.WinUI
{
    public static class Program
    {
        private const string WebViewFolderVariable = "WEBVIEW2_USER_DATA_FOLDER";

        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().SetAutoApplyOnStartup(true).Run();

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(WebViewFolderVariable)))
                Environment.SetEnvironmentVariable(WebViewFolderVariable, AppPaths.WebViewFolder);

            ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
    }
}

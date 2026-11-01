using System;
using DocuHelperEditor.State;
using Microsoft.Extensions.DependencyInjection;
using WinRT.Interop;

namespace DocuHelperEditor.WinUI
{
    public static class CloseGuard
    {
        private static bool s_attached;

        public static void Attach(Microsoft.UI.Xaml.Window window, IServiceProvider services)
        {
            if (s_attached)
                return;

            var workspace = services.GetService<WorkspaceStore>();
            var assistant = services.GetService<AssistantStore>();
            if (workspace == null || assistant == null)
                return;

            s_attached = true;
            var hwnd = WindowNative.GetWindowHandle(window);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
            appWindow.Closing += (_, args) =>
            {
                if (workspace.IsAppCloseAllowed || (workspace.UnsavedCount == 0 && !assistant.IsRunning))
                    return;

                args.Cancel = true;
                workspace.AskCloseApp();
            };

            workspace.QuitRequested += () => Microsoft.Maui.Controls.Application.Current?.Quit();
        }
    }
}

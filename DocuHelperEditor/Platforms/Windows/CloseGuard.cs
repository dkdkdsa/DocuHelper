using System;
using DocuHelperEditor.Services;
using DocuHelperEditor.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using WinRT.Interop;

namespace DocuHelperEditor.WinUI
{
    public static class CloseGuard
    {
        private static readonly TimeSpan s_dialogTimeout = TimeSpan.FromSeconds(3);
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
            var queue = DispatcherQueue.GetForCurrentThread();
            var hwnd = WindowNative.GetWindowHandle(window);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
            appWindow.Closing += (_, args) =>
            {
                try
                {
                    if (workspace.IsAppCloseAllowed)
                        return;

                    if (workspace.IsClosingApp)
                    {
                        args.Cancel = true;
                        return;
                    }

                    if (workspace.TakeForceCloseRequest())
                    {
                        assistant.Stop();
                        workspace.AllowAppClose();
                        return;
                    }

                    if (workspace.UnsavedCount == 0 && !assistant.IsRunning)
                        return;

                    args.Cancel = true;
                    workspace.AskCloseApp();
                    ArmDialogTimeout(queue, workspace);
                }
                catch (Exception e)
                {
                    AppLog.Write("Close guard failed", e);
                    args.Cancel = false;
                }
            };

            workspace.QuitRequested += () => Microsoft.Maui.Controls.Application.Current?.Quit();
        }

        private static void ArmDialogTimeout(DispatcherQueue queue, WorkspaceStore workspace)
        {
            var timer = queue.CreateTimer();
            timer.Interval = s_dialogTimeout;
            timer.IsRepeating = false;
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    workspace.CheckCloseDialogShown();
                }
                catch (Exception e)
                {
                    AppLog.Write("Close dialog timeout failed", e);
                }
            };
            timer.Start();
        }
    }
}

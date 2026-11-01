using DocuHelperEditor.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace DocuHelperEditor
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = new Window(new MainPage())
            {
                Title = "문서 도우미",
                Width = 1440,
                Height = 900,
                MinimumWidth = 960,
                MinimumHeight = 640
            };

            var services = activationState.Context.Services;
            var updates = services.GetService<UpdateStore>();
            window.Destroying += (_, _) => updates?.ApplyPendingOnExit();
#if WINDOWS
            window.HandlerChanged += (_, _) =>
            {
                if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
                    DocuHelperEditor.WinUI.CloseGuard.Attach(native, services);
            };
#endif
            return window;
        }
    }
}

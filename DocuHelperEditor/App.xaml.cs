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
            return new Window(new MainPage())
            {
                Title = "문서 도우미",
                Width = 1440,
                Height = 900,
                MinimumWidth = 960,
                MinimumHeight = 640
            };
        }
    }
}

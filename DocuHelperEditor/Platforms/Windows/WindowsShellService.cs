using System.Diagnostics;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.WinUI
{
    public class WindowsShellService : IShellService
    {
        public void OpenFile(string path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        public void RevealInExplorer(string path)
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
    }
}

using System.Diagnostics;
using DocuHelperEditor.Services;
using Microsoft.VisualBasic.FileIO;

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

        public void MoveToRecycleBin(string path)
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
    }
}

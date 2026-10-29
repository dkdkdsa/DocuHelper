namespace DocuHelperEditor.Services
{
    public interface IShellService
    {
        void OpenFile(string path);

        void RevealInExplorer(string path);
    }
}

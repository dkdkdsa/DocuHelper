#if DEBUG
namespace DocuHelperEditor.Services
{
    public sealed class RecordingShellService : IShellService
    {
        public const string EnableVariable = "DOCUHELPER_FAKE_SHELL";

        public static bool IsRequested => !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(EnableVariable));

        public void OpenFile(string path) => AppLog.Write("Shell.OpenFile " + path);

        public void RevealInExplorer(string path) => AppLog.Write("Shell.RevealInExplorer " + path);

        public void MoveToRecycleBin(string path) => AppLog.Write("Shell.MoveToRecycleBin " + path);
    }
}
#endif

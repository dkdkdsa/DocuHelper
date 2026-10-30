using System;
using System.IO;

namespace DocuHelperEditor.Services
{
    public static class AppPaths
    {
        private const string AppName = "문서도우미";
        private const string HomeVariable = "DOCUHELPER_HOME";

        private static readonly string s_home = Environment.GetEnvironmentVariable(HomeVariable);

        public static string DataFolder { get; } = string.IsNullOrWhiteSpace(s_home)
            ? AppContext.BaseDirectory
            : Path.Combine(s_home, "data");

        public static string LogFolder { get; } = string.IsNullOrWhiteSpace(s_home)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName)
            : Path.Combine(s_home, "data");

        public static string DocumentsFolder { get; } = string.IsNullOrWhiteSpace(s_home)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : Path.Combine(s_home, "documents");

        public static string DesktopFolder { get; } = string.IsNullOrWhiteSpace(s_home)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : Path.Combine(s_home, "desktop");

        public static string DefaultRoot { get; } = Path.Combine(DocumentsFolder, AppName);

        public static string Display(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            if (StartsWithFolder(path, DocumentsFolder))
                return "문서" + path.Substring(DocumentsFolder.Length);

            if (StartsWithFolder(path, DesktopFolder))
                return "바탕 화면" + path.Substring(DesktopFolder.Length);

            return path;
        }

        private static bool StartsWithFolder(string path, string folder) =>
            !string.IsNullOrEmpty(folder)
            && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
            && (path.Length == folder.Length || path[folder.Length] == Path.DirectorySeparatorChar);
    }
}

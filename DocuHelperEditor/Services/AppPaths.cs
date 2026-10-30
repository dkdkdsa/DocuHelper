using System;
using System.IO;

namespace DocuHelperEditor.Services
{
    public static class AppPaths
    {
        private const string AppName = "문서도우미";
        private const string HomeVariable = "DOCUHELPER_HOME";

        private static readonly string s_home = Environment.GetEnvironmentVariable(HomeVariable);
        private static readonly string s_userFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        private static readonly string[] s_legacyFiles = { "key.dat", "settings.json", "workspaces.json" };

        public static string DataFolder { get; } = string.IsNullOrWhiteSpace(s_home)
            ? s_userFolder
            : Path.Combine(s_home, "data");

        public static string LogFolder { get; } = string.IsNullOrWhiteSpace(s_home)
            ? s_userFolder
            : Path.Combine(s_home, "data");

        public static string WebViewFolder { get; } = Path.Combine(DataFolder, "WebView2");

        public static string DocumentsFolder { get; } = string.IsNullOrWhiteSpace(s_home)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : Path.Combine(s_home, "documents");

        public static string DesktopFolder { get; } = string.IsNullOrWhiteSpace(s_home)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : Path.Combine(s_home, "desktop");

        public static string DefaultRoot { get; } = Path.Combine(DocumentsFolder, AppName);

        public static void CopyLegacyData()
        {
            if (!string.IsNullOrWhiteSpace(s_home))
                return;

            foreach (var name in s_legacyFiles)
            {
                var source = Path.Combine(AppContext.BaseDirectory, name);
                var target = Path.Combine(DataFolder, name);
                if (!File.Exists(source) || File.Exists(target))
                    continue;

                try
                {
                    Directory.CreateDirectory(DataFolder);
                    File.Copy(source, target);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    AppLog.Write($"Could not copy {name} to the data folder", e);
                }
            }
        }

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

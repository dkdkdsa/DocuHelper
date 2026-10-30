using System;
using System.IO;

namespace DocuHelperEditor.Services
{
    public static class AppLog
    {
        private static readonly object s_lock = new();

        public static string FilePath { get; } = Path.Combine(AppPaths.LogFolder, "log.txt");

        public static void Write(string message, Exception exception = null)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
            if (exception != null)
                line += Environment.NewLine + exception;

            try
            {
                lock (s_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    File.AppendAllText(FilePath, line + Environment.NewLine);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

using System;
using System.IO;
using System.Text;

namespace DocuHelperEditor.Services
{
    public static class AppLog
    {
        private static readonly object s_lock = new();

        public static string FilePath { get; } = Path.Combine(AppPaths.LogFolder, "log.txt");

        public static void Write(string message, Exception exception = null)
        {
            var line = new StringBuilder($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
            if (exception != null)
                line.Append(Environment.NewLine).Append(exception);

            try
            {
                lock (s_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    File.AppendAllText(FilePath, line.AppendLine().ToString());
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

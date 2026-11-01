using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Samples;

namespace DocuHelperEditor.Services
{
    public static class SessionRepository
    {
        private const long MaxBytes = 1024 * 1024;
        private const int MaxFiles = 200;

        private static readonly JsonSerializerOptions s_json = new() { WriteIndented = true };

        public static void Save(string folderPath, SavedSession session)
        {
            if (SampleScenarios.Current != null)
                return;

            var path = PathOf(folderPath);
            if (path == null)
                return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonSerializer.Serialize(session, s_json));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not save the session", e);
            }
        }

        public static SavedSession Load(string folderPath)
        {
            var path = PathOf(folderPath);
            if (path == null || !File.Exists(path))
                return null;

            try
            {
                if (new FileInfo(path).Length > MaxBytes)
                    return null;

                var session = JsonSerializer.Deserialize<SavedSession>(File.ReadAllText(path));
                if (session?.OpenFiles != null)
                    session.OpenFiles = session.OpenFiles.Where(name => !string.IsNullOrEmpty(name)).Take(MaxFiles).ToList();

                return session;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                AppLog.Write("Could not read the session", e);
                return null;
            }
        }

        private static string PathOf(string folderPath) => string.IsNullOrEmpty(folderPath)
            ? null
            : Path.Combine(folderPath, ".문서도우미", "세션.json");
    }
}

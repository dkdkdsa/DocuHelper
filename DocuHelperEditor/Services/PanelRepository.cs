using System;
using System.IO;
using System.Text.Json;
using DocuHelperEditor.State;

namespace DocuHelperEditor.Services
{
    public static class PanelRepository
    {
        private const long MaxBytes = 4096;

        private static readonly JsonSerializerOptions s_json = new() { WriteIndented = true };

        public static PanelState Load(string folderPath)
        {
            var path = PathOf(folderPath);
            if (path == null || !File.Exists(path))
                return new PanelState();

            try
            {
                if (new FileInfo(path).Length > MaxBytes)
                    return new PanelState();

                return JsonSerializer.Deserialize<PanelState>(File.ReadAllText(path)) ?? new PanelState();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                AppLog.Write("Could not read the panel state", e);
                return new PanelState();
            }
        }

        public static void SaveFolder(string folderPath, bool collapsed) => Update(folderPath, state => state.FolderCollapsed = collapsed);

        public static void SaveAssistant(string folderPath, bool collapsed) => Update(folderPath, state => state.AssistantCollapsed = collapsed);

        private static void Update(string folderPath, Action<PanelState> change)
        {
            var path = PathOf(folderPath);
            if (path == null)
                return;

            var state = Load(folderPath);
            change(state);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonSerializer.Serialize(state, s_json));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not save the panel state", e);
            }
        }

        private static string PathOf(string folderPath) => string.IsNullOrEmpty(folderPath)
            ? null
            : Path.Combine(folderPath, ".문서도우미", "패널.json");
    }
}

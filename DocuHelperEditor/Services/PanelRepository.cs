using System;
using System.IO;
using System.Security;
using System.Text.Json;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Samples;

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
            catch (Exception e) when (IsStorageError(e))
            {
                AppLog.Write("Could not read the panel state", e);
                return new PanelState();
            }
        }

        public static void SaveFolder(string folderPath, bool collapsed) => Update(folderPath, state => state.FolderCollapsed = collapsed);

        public static void SaveAssistant(string folderPath, bool collapsed) => Update(folderPath, state => state.AssistantCollapsed = collapsed);

        private static void Update(string folderPath, Action<PanelState> change)
        {
            if (SampleScenarios.Current != null)
                return;

            var path = PathOf(folderPath);
            if (path == null)
                return;

            var state = Load(folderPath);
            change(state);
            var temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp, JsonSerializer.Serialize(state, s_json));
                File.Move(temp, path, overwrite: true);
            }
            catch (Exception e) when (IsStorageError(e))
            {
                AppLog.Write("Could not save the panel state", e);
                DeleteQuietly(temp);
            }
        }

        private static void DeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e) when (IsStorageError(e))
            {
                AppLog.Write("Could not remove the panel state temp file", e);
            }
        }

        private static bool IsStorageError(Exception e) => e is IOException
            || e is UnauthorizedAccessException
            || e is NotSupportedException
            || e is SecurityException
            || e is ArgumentException
            || e is JsonException;

        private static string PathOf(string folderPath) => string.IsNullOrEmpty(folderPath)
            ? null
            : Path.Combine(folderPath, ".문서도우미", "패널.json");
    }
}

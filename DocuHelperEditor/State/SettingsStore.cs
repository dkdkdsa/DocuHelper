using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DocuHelper;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class SettingsStore
    {
        private const string KeyName = "key";

        private static readonly string s_settingsPath = Path.Combine(AppPaths.DataFolder, "settings.json");

        private readonly Workspace _workspace;
        private readonly IFileDialogService _dialogs;
        private readonly ISecretStore _secrets;
        private bool _loaded;
        private string _rootPath;

        public SettingsStore(Workspace workspace, IFileDialogService dialogs, IEnumerable<ISecretStore> secrets)
        {
            _workspace = workspace;
            _dialogs = dialogs;
            _secrets = secrets.FirstOrDefault();
        }

        public event Action Changed;

        public string ApiKey { get; private set; } = string.Empty;

        public bool HasSavedKey { get; private set; }

        public bool IsKeyVisible { get; private set; }

        public KeyCheckState KeyState { get; private set; }

        public string WorkspaceRoot { get; private set; } = @"문서\문서도우미";

        public bool TrackChanges { get; private set; }

        public string ModelId { get; private set; } = AssistantModels.DefaultModel;

        public string EffortId { get; private set; } = AssistantModels.DefaultEffort;

        public ModelOption Model => AssistantModels.FindModel(ModelId);

        public EffortOption Effort => AssistantModels.FindEffort(EffortId);

        public bool CanStart => !string.IsNullOrWhiteSpace(ApiKey);

        public bool IsConfigured => HasSavedKey;

        public string RootPath => _rootPath ?? AppPaths.DefaultRoot;

        public void Load(string apiKey, KeyCheckState keyState, string workspaceRoot)
        {
            ApiKey = apiKey ?? string.Empty;
            KeyState = keyState;
            WorkspaceRoot = workspaceRoot;
            IsKeyVisible = false;
            Notify();
        }

        public void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            Reload();
        }

        public void Reload()
        {
            var data = ReadSettings();
            _rootPath = string.IsNullOrWhiteSpace(data?.Root) ? null : data.Root;
            ModelId = AssistantModels.FindModel(data?.Model).Id;
            EffortId = AssistantModels.FindEffort(data?.Effort).Id;
            TrackChanges = data?.TrackChanges ?? false;
            ApiKey = _secrets?.Load(KeyName) ?? string.Empty;
            HasSavedKey = !string.IsNullOrWhiteSpace(ApiKey);
            KeyState = KeyCheckState.Idle;
            IsKeyVisible = false;
            WorkspaceRoot = AppPaths.Display(RootPath);
            Notify();
        }

        public void SetApiKey(string apiKey)
        {
            ApiKey = apiKey ?? string.Empty;
            KeyState = KeyCheckState.Idle;
            Notify();
        }

        public void ToggleKeyVisible()
        {
            IsKeyVisible = !IsKeyVisible;
            Notify();
        }

        public void SetTrackChanges(bool trackChanges)
        {
            TrackChanges = trackChanges;
            WriteSettings();
            Notify();
        }

        public void SetModel(string modelId)
        {
            ModelId = AssistantModels.FindModel(modelId).Id;
            WriteSettings();
            Notify();
        }

        public void SetEffort(string effortId)
        {
            EffortId = AssistantModels.FindEffort(effortId).Id;
            WriteSettings();
            Notify();
        }

        public async Task VerifyKeyAsync()
        {
            if (string.IsNullOrWhiteSpace(ApiKey) || KeyState == KeyCheckState.Checking)
                return;

            var key = ApiKey;
            KeyState = KeyCheckState.Checking;
            Notify();

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await _workspace.Agent.VerifyKeyAsync(key, timeout.Token);
                if (key == ApiKey)
                    KeyState = KeyCheckState.Ok;
            }
            catch (Exception e)
            {
                AppLog.Write("Key check failed", e);
                if (key == ApiKey)
                    KeyState = KeyCheckState.Failed;
            }

            Notify();
        }

        public async Task ChangeWorkspaceRootAsync()
        {
            var path = await _dialogs.PickFolderAsync();
            if (string.IsNullOrEmpty(path))
                return;

            _rootPath = path;
            WorkspaceRoot = AppPaths.Display(path);
            WriteSettings();
            Notify();
        }

        public bool SaveKey()
        {
            var key = ApiKey.Trim();
            try
            {
                _secrets?.Save(KeyName, key);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not save the key", e);
                return false;
            }

            ApiKey = key;
            HasSavedKey = key.Length > 0;
            _loaded = true;
            WriteSettings();
            Notify();
            return true;
        }

        public void Notify() => Changed?.Invoke();

        private void WriteSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(s_settingsPath));
                var data = new SettingsData { Root = _rootPath, Model = ModelId, Effort = EffortId, TrackChanges = TrackChanges };
                File.WriteAllText(s_settingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not save settings", e);
            }
        }

        private static SettingsData ReadSettings()
        {
            try
            {
                return File.Exists(s_settingsPath) ? JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(s_settingsPath)) : null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                AppLog.Write("Could not read settings", e);
                return null;
            }
        }
    }
}

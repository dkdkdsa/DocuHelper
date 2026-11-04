using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocuHelper;
using DocuHelper.Agents;
using DocuHelper.Docus;
using DocuHelper.Editors;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Samples;

namespace DocuHelperEditor.State
{
    public partial class SettingsStore
    {
        public const string DefaultTextSize = "m";

        private const string DefaultSection = "key";

        private static readonly string[] s_textSizes = { "m", "l", "xl" };
        private static readonly string[] s_sections = { "key", "ai", "usage", "folder", "hwp", "view", "update", "about" };

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

        public bool IsKeyVisible { get; private set; }

        public string WorkspaceRoot { get; private set; } = @"문서\문서도우미";

        public bool TrackChanges { get; private set; }

        public string ModelId { get; private set; } = AssistantModels.DefaultModel;

        public string EffortId { get; private set; } = AssistantModels.DefaultEffort;

        public bool AskBigChanges { get; private set; } = true;

        public bool OpenLastFolder { get; private set; }

        public bool HwpAlwaysHwpx { get; private set; }

        public bool HwpNewAsHwp { get; private set; }

        public string TextSize { get; private set; } = DefaultTextSize;

        public string LastRunVersion { get; private set; }

        public bool UpdateCheck { get; private set; } = true;

        public bool UpdateAuto { get; private set; } = true;

        public bool IsHancomInstalled => HwpEditor?.CanWriteHwp == true;

        public bool IsDialogOpen { get; private set; }

        public string DialogSection { get; private set; } = DefaultSection;

        public string DialogReturnFocus { get; private set; }

        public ModelOption Model => ResolveModel();

        public IEnumerable<ModelOption> AvailableModels => AssistantModels.Models.Where(model => HasKey(model.Provider));

        public EffortOption Effort => AssistantModels.FindEffort(EffortId);

        public string RootPath => _rootPath ?? AppPaths.DefaultRoot;

        private HwpEditor HwpEditor => _workspace.Editors.TryGetValue(DocuType.Hwp, out var editor) ? editor as HwpEditor : null;

        public void Load(string apiKey, KeyCheckState keyState, string workspaceRoot)
        {
            LoadSampleKey(apiKey, keyState);
            WorkspaceRoot = workspaceRoot;
            IsKeyVisible = false;
            Notify();
        }

        public void EnsureLoaded()
        {
            if (_loaded)
                return;

            Reload();
            _loaded = true;
        }

        public void Reload()
        {
            var data = ReadSettings();
            _rootPath = string.IsNullOrWhiteSpace(data?.Root) ? null : data.Root;
            ModelId = AssistantModels.FindModel(data?.Model).Id;
            EffortId = AssistantModels.FindEffort(data?.Effort).Id;
            TrackChanges = data?.TrackChanges ?? false;
            AskBigChanges = data?.AskBigChanges ?? true;
            OpenLastFolder = data?.OpenLastFolder ?? false;
            HwpAlwaysHwpx = data?.HwpAlwaysHwpx ?? false;
            HwpNewAsHwp = data?.HwpNewAsHwp ?? false;
            TextSize = Array.IndexOf(s_textSizes, data?.TextSize) >= 0 ? data.TextSize : DefaultTextSize;
            LastRunVersion = data?.LastRunVersion;
            UpdateCheck = data?.UpdateCheck ?? true;
            UpdateAuto = data?.UpdateAuto ?? true;
            LoadUsage(data);
            LoadContext(data);
            ApplyHwp();
            LoadKeys(data);
            IsKeyVisible = false;
            WorkspaceRoot = AppPaths.Display(RootPath);
            Notify();
        }

        public void OpenDialog(string returnFocus, string section = null)
        {
            EnsureLoaded();
            DialogReturnFocus = returnFocus;
            DialogSection = Array.IndexOf(s_sections, section) >= 0 ? section : DefaultSection;
            IsDialogOpen = true;
            CancelKeyInteraction();
            Notify();
        }

        public void CloseDialog()
        {
            IsDialogOpen = false;
            CancelKeyInteraction();
            Notify();
        }

        public void ShowSection(string section)
        {
            if (Array.IndexOf(s_sections, section) < 0)
                return;

            DialogSection = section;
            Notify();
        }

        public void SetAskBigChanges(bool ask)
        {
            AskBigChanges = ask;
            WriteSettings();
            Notify();
        }

        public void SetOpenLastFolder(bool openLast)
        {
            OpenLastFolder = openLast;
            WriteSettings();
            Notify();
        }

        public void SetHwpAlwaysHwpx(bool alwaysHwpx)
        {
            HwpAlwaysHwpx = alwaysHwpx;
            ApplyHwp();
            WriteSettings();
            Notify();
        }

        public void SetHwpNewAsHwp(bool newAsHwp)
        {
            HwpNewAsHwp = newAsHwp;
            ApplyHwp();
            WriteSettings();
            Notify();
        }

        public void SetUpdateCheck(bool check)
        {
            UpdateCheck = check;
            WriteSettings();
            Notify();
        }

        public void SetUpdateAuto(bool auto)
        {
            UpdateAuto = auto;
            WriteSettings();
            Notify();
        }

        public void SetLastRunVersion(string version)
        {
            LastRunVersion = version;
            WriteSettings();
        }

        public void SetTextSize(string size)
        {
            if (Array.IndexOf(s_textSizes, size) < 0)
                return;

            TextSize = size;
            WriteSettings();
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
            var model = AssistantModels.FindModel(modelId);
            if (!HasKey(model.Provider))
                return;

            ModelId = model.Id;
            WriteSettings();
            Notify();
        }

        public void SetEffort(string effortId)
        {
            EffortId = AssistantModels.FindEffort(effortId).Id;
            WriteSettings();
            Notify();
        }

        public void ToggleKeyVisible()
        {
            IsKeyVisible = !IsKeyVisible;
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

        public void Notify() => Changed?.Invoke();

        private void WriteSettings()
        {
            if (SampleScenarios.Current != null)
                return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(s_settingsPath));
                var data = new SettingsData
                {
                    Root = _rootPath,
                    Model = ModelId,
                    Effort = EffortId,
                    TrackChanges = TrackChanges,
                    AskBigChanges = AskBigChanges,
                    OpenLastFolder = OpenLastFolder,
                    HwpAlwaysHwpx = HwpAlwaysHwpx,
                    HwpNewAsHwp = HwpNewAsHwp,
                    TextSize = TextSize,
                    LastRunVersion = LastRunVersion,
                    UpdateCheck = UpdateCheck,
                    UpdateAuto = UpdateAuto,
                    ClaudeCheckedAt = SlotFor(AgentProvider.Claude).CheckedAt,
                    OpenAiCheckedAt = SlotFor(AgentProvider.OpenAi).CheckedAt,
                    UsageCap = UsageCap,
                    UsageAlert = UsageAlertPercent,
                    UsageAtCap = UsageAtCap,
                    UsageShowCost = UsageShowCost,
                    UsdRate = UsdRate,
                    TrimAbove = TrimAbove,
                };
                var temp = s_settingsPath + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, s_settingsPath, true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not save settings", e);
            }
        }

        private void ApplyHwp()
        {
            var editor = HwpEditor;
            if (editor == null)
                return;

            editor.AlwaysHwpx = HwpAlwaysHwpx;
            editor.NewAsHwp = HwpNewAsHwp;
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

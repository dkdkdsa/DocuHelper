using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper;
using DocuHelper.Importers;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Mapping;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private readonly Workspace _workspace;
        private readonly IReadOnlyList<DocuImporter> _importers;
        private readonly IReadOnlyList<IPaperMapper> _mappers;
        private readonly SettingsStore _settings;
        private readonly IFileDialogService _dialogs;
        private readonly IShellService _shell;
        private readonly WorkspaceRegistry _registry;
        private readonly List<WorkspaceFile> _files = new();
        private readonly List<string> _openIds = new();
        private readonly List<SaveHistoryEntry> _history = new();

        public WorkspaceStore(
            Workspace workspace,
            IEnumerable<DocuImporter> importers,
            IEnumerable<IPaperMapper> mappers,
            SettingsStore settings,
            IFileDialogService dialogs,
            IEnumerable<IShellService> shells,
            WorkspaceRegistry registry)
        {
            _workspace = workspace;
            _importers = importers.ToList();
            _mappers = mappers.ToList();
            _settings = settings;
            _dialogs = dialogs;
            _shell = shells.FirstOrDefault();
            _registry = registry;
            _workspace.DocusChanged += OnDocusChanged;
            _workspace.DocuCreated += OnDocuCreated;
        }

        public event Action Changed;

        public Workspace Workspace => _workspace;

        public IReadOnlyList<DocuImporter> Importers => _importers;

        public string FolderName { get; private set; } = "내 작업 폴더";

        public string FolderPath { get; private set; }

        public IReadOnlyList<WorkspaceFile> Files => _files;

        public IReadOnlyList<WorkspaceFile> OpenFiles => _openIds.Select(FindFile).Where(f => f != null).ToList();

        public WorkspaceFile ActiveFile { get; private set; }

        public bool IsFolderCollapsed { get; private set; }

        public string SelectionLabel { get; private set; }

        public string SelectedChangeId { get; private set; }

        public int ChangePickVersion { get; private set; }

        public bool IsHistoryOpen { get; private set; }

        public bool IsToastVisible { get; private set; }

        public int ToastVersion { get; private set; }

        public bool HasSavedAll { get; private set; }

        public WorkspaceFile PendingClose { get; private set; }

        public int UnsavedCount => _files.Count(f => f.IsDirty);

        public bool IsAnyFileEditing => _files.Any(f => f.Activity == FileActivity.Editing);

        public bool CanSaveAll => UnsavedCount > 0 && !IsAnyFileEditing;

        public string SaveStatusText => UnsavedCount > 0
            ? $"저장 안 한 파일 {UnsavedCount}개"
            : HasSavedAll ? "모두 저장했어요" : "바뀐 것 없음";

        public IReadOnlyList<SaveHistoryEntry> ActiveHistory => ActiveFile == null
            ? Array.Empty<SaveHistoryEntry>()
            : _history.Where(h => h.FileId == ActiveFile.Id).ToList();

        public ChangeItem SelectedChange => ActiveFile?.Changes.FirstOrDefault(c => c.Id == SelectedChangeId);

        public int SelectedChangeIndex => ActiveFile == null ? -1 : ActiveFile.Changes.FindIndex(c => c.Id == SelectedChangeId);

        public WorkspaceFile FindFile(string fileId) => _files.FirstOrDefault(f => f.Id == fileId);

        public void Load(string folderName, string folderPath, IEnumerable<WorkspaceFile> files, IEnumerable<string> openIds, string activeId)
        {
            DetachAll();
            IsReady = false;
            FolderName = folderName;
            FolderPath = folderPath;
            _files.AddRange(files);
            _openIds.AddRange(openIds.Where(id => FindFile(id) != null));
            ActiveFile = FindFile(activeId);
            SelectionLabel = null;
            SelectedChangeId = null;
            IsHistoryOpen = false;
            IsToastVisible = false;
            HasSavedAll = false;
            PendingClose = null;
            Notify();
        }

        public void LoadHistory(IEnumerable<SaveHistoryEntry> entries)
        {
            _history.Clear();
            _history.AddRange(entries);
            Notify();
        }

        public void Open(string fileId)
        {
            var file = FindFile(fileId);
            if (file == null)
                return;

            if (!_openIds.Contains(fileId))
                _openIds.Add(fileId);

            if (ActiveFile != file)
            {
                ActiveFile = file;
                SelectionLabel = null;
                ReloadHistory();
            }

            Notify();
        }

        public void RequestClose(string fileId)
        {
            var file = FindFile(fileId);
            if (file == null)
                return;

            if (file.IsDirty)
            {
                PendingClose = file;
                Notify();
                return;
            }

            CloseTab(fileId);
        }

        public void CancelClose()
        {
            PendingClose = null;
            Notify();
        }

        public async Task CloseWithSaveAsync()
        {
            var file = PendingClose;
            if (file == null)
                return;

            await SaveAsync(file.Id);
            if (file.IsDirty)
                return;

            PendingClose = null;
            CloseTab(file.Id);
        }

        public void CloseWithoutSave()
        {
            var file = PendingClose;
            if (file == null)
                return;

            PendingClose = null;
            if (file.Docu == null)
            {
                file.IsDirty = false;
                file.Changes.Clear();
            }
            else if (!file.Docu.IsLocked)
            {
                _workspace.Discard(file.Docu);
            }

            CloseTab(file.Id);
        }

        public void ToggleFolder()
        {
            IsFolderCollapsed = !IsFolderCollapsed;
            Notify();
        }

        public void SetFolderCollapsed(bool collapsed)
        {
            IsFolderCollapsed = collapsed;
            Notify();
        }

        public void SetSelection(string label)
        {
            SelectionLabel = label;
            Notify();
        }

        public void SelectChange(string changeId)
        {
            var file = _files.FirstOrDefault(f => f.Changes.Any(c => c.Id == changeId));
            if (file == null)
                return;

            if (!_openIds.Contains(file.Id))
                _openIds.Add(file.Id);

            if (ActiveFile != file)
            {
                ActiveFile = file;
                SelectionLabel = null;
                ReloadHistory();
            }

            SelectedChangeId = changeId;
            ChangePickVersion++;
            Notify();
        }

        public void MoveChange(int step)
        {
            if (ActiveFile == null || ActiveFile.Changes.Count == 0)
                return;

            var count = ActiveFile.Changes.Count;
            var index = SelectedChangeIndex;
            var next = index < 0
                ? (step > 0 ? 0 : count - 1)
                : ((index + step) % count + count) % count;

            SelectedChangeId = ActiveFile.Changes[next].Id;
            ChangePickVersion++;
            Notify();
        }

        public void ToggleHistory()
        {
            IsHistoryOpen = !IsHistoryOpen;
            if (IsHistoryOpen)
                ReloadHistory();

            Notify();
        }

        public void CloseHistory()
        {
            IsHistoryOpen = false;
            Notify();
        }

        public void SetSavedAll(bool savedAll)
        {
            HasSavedAll = savedAll;
            Notify();
        }

        public void ShowToast()
        {
            IsToastVisible = true;
            ToastVersion++;
            Notify();
        }

        public void DismissToast()
        {
            IsToastVisible = false;
            Notify();
        }

        public void Notify() => Changed?.Invoke();

        private void CloseTab(string fileId)
        {
            var index = _openIds.IndexOf(fileId);
            if (index < 0)
                return;

            _openIds.RemoveAt(index);

            if (ActiveFile?.Id == fileId)
            {
                ActiveFile = _openIds.Count == 0 ? null : FindFile(_openIds[Math.Min(index, _openIds.Count - 1)]);
                SelectionLabel = null;
                ReloadHistory();
            }

            Notify();
        }
    }
}

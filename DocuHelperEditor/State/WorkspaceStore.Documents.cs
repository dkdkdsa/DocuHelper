using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelper.Editors;
using DocuHelper.Importers;
using DocuHelper.Importers.Hwp;
using DocuHelper.Tools;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private static readonly CultureInfo s_korean = CultureInfo.GetCultureInfo("ko-KR");

        private readonly HashSet<string> _savedChangeIds = new();

        public event Action<string> NoticeRaised;

        public event Action FolderOpened;

        public bool IsReady { get; private set; }

        public string RenamingFileId { get; private set; }

        public string AskingFileId { get; private set; }

        public bool CanUndo => IsReady && _workspace.CanUndo;

        public bool CanRedo => IsReady && _workspace.CanRedo;

        public bool IsLiveChange(string changeId) => _files.Any(f => f.Changes.Any(c => c.Id == changeId));

        public bool IsSavedChange(string changeId) => _savedChangeIds.Contains(changeId);

        public void OpenFolder(string folderPath)
        {
            DetachAll();
            _openIds.Clear();
            _history.Clear();
            ActiveFile = null;
            SelectionLabel = null;
            SelectedChangeId = null;
            IsHistoryOpen = false;
            IsToastVisible = false;
            HasSavedAll = false;
            PendingClose = null;
            AskingFileId = null;
            RenamingFileId = null;

            _workspace.OpenFolder(folderPath);
            FolderPath = _workspace.FolderPath;
            FolderName = Path.GetFileName(FolderPath);
            FolderOpened?.Invoke();

            var failed = new List<string>();
            foreach (var path in _workspace.ListFolderFiles())
            {
                var importer = FindImporter(path);
                if (importer == null)
                    continue;

                try
                {
                    var docu = importer.Import(path);
                    _workspace.AddDocu(docu);
                    _workspace.RecordInitial(docu);
                }
                catch (Exception e)
                {
                    AppLog.Write($"Could not open {path}", e);
                    failed.Add(Path.GetFileName(path));
                }
            }

            IsReady = true;
            var first = _files.FirstOrDefault();
            if (first != null)
                Open(first.Id);

            if (failed.Count > 0)
                RaiseNotice($"‘{string.Join("’, ‘", failed)}’ 파일은 열지 못했어요. 파일이 망가졌거나 암호가 걸려 있을 수 있어요.");

            Notify();
        }

        public void SetAskingFile(string fileId)
        {
            AskingFileId = fileId;
            foreach (var file in _files.Where(f => f.Docu != null))
                file.Activity = ActivityOf(file);

            Notify();
        }

        public ToolResult ApplyEdit(WorkspaceFile file, EditCommand command)
        {
            if (file?.Docu == null)
                return ToolResult.Fail("이 화면은 예시라서 고칠 수 없어요.");

            var result = _workspace.Apply(file.Docu, command);
            if (result.IsError)
                AppLog.Write($"Hand edit '{command.Name}' failed on {file.Name}: {result.Content}");

            return result;
        }

        public Task ImportAsync(IEnumerable<string> paths) => ImportAsync(paths, new List<string>());

        private async Task ImportAsync(IEnumerable<string> paths, List<string> problems)
        {
            if (!IsReady || paths == null)
                return;

            var converted = new List<string>();
            WorkspaceFile last = null;
            foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var candidates = Directory.Exists(path)
                    ? Directory.EnumerateFiles(path).Where(p => FindImporter(p) != null || HwpConverter.CanConvert(p)).ToList()
                    : new List<string> { path };

                foreach (var candidate in candidates)
                {
                    var file = HwpConverter.CanConvert(candidate)
                        ? await ConvertFileAsync(candidate, problems, converted)
                        : await ImportFileAsync(candidate, problems);
                    if (file != null)
                        last = file;
                }
            }

            if (last != null)
                Open(last.Id);

            if (converted.Count > 0)
                converted.Add("원래 한글 파일은 그대로 있고, 글자 모양이나 표는 원래와 조금 다를 수 있어요.");

            var notes = converted.Concat(problems.Distinct()).ToList();
            if (notes.Count > 0)
                RaiseNotice(string.Join("\n", notes));
        }

        public Task SaveAsync(string fileId)
        {
            var file = FindFile(fileId);
            if (file == null || !file.IsDirty)
                return Task.CompletedTask;

            if (file.Docu == null)
            {
                _savedChangeIds.UnionWith(file.Changes.Select(c => c.Id));
                file.IsDirty = false;
                file.Changes.Clear();
                ShowToast();
                return Task.CompletedTask;
            }

            if (file.Docu.IsLocked || !TrySave(file))
                return Task.CompletedTask;

            HasSavedAll = UnsavedCount == 0;
            ReloadHistory();
            ShowToast();
            return Task.CompletedTask;
        }

        public Task SaveAllAsync()
        {
            if (!CanSaveAll)
                return Task.CompletedTask;

            var saved = 0;
            var failed = false;
            foreach (var file in _files.Where(f => f.IsDirty).ToList())
            {
                if (file.Docu == null)
                {
                    _savedChangeIds.UnionWith(file.Changes.Select(c => c.Id));
                    file.IsDirty = false;
                    file.Changes.Clear();
                    saved++;
                    continue;
                }

                if (file.Docu.IsLocked)
                    continue;

                if (TrySave(file))
                    saved++;
                else
                    failed = true;
            }

            if (saved > 0)
            {
                HasSavedAll = !failed && UnsavedCount == 0;
                ReloadHistory();
                ShowToast();
            }

            Notify();
            return Task.CompletedTask;
        }

        public Task RevertAllAsync(string fileId)
        {
            var file = FindFile(fileId);
            if (file == null)
                return Task.CompletedTask;

            if (file.Docu == null)
            {
                file.Changes.Clear();
                Notify();
                return Task.CompletedTask;
            }

            _workspace.RevertAssistantChanges(file.Docu);
            return Task.CompletedTask;
        }

        public Task RestoreAsync(string historyId)
        {
            var file = ActiveFile;
            if (file?.Docu == null || file.Docu.IsLocked || _workspace.History == null)
                return Task.CompletedTask;

            var entry = _workspace.History.List(file.Docu.Name).FirstOrDefault(e => e.Id == historyId);
            if (entry == null)
                return Task.CompletedTask;

            try
            {
                _workspace.Restore(file.Docu, entry);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write($"Restore failed for {file.Name}", e);
                RaiseNotice($"‘{file.Name}’ 파일이 다른 프로그램에서 열려 있어서 되돌리지 못했어요. 그 프로그램을 닫고 다시 해 주세요.");
            }

            ReloadHistory();
            Notify();
            return Task.CompletedTask;
        }

        public Task UndoAsync()
        {
            if (IsReady && !_workspace.IsAgentRunning)
                _workspace.Undo();

            return Task.CompletedTask;
        }

        public Task RedoAsync()
        {
            if (IsReady && !_workspace.IsAgentRunning)
                _workspace.Redo();

            return Task.CompletedTask;
        }


        public async Task SaveCopyAsync(string fileId)
        {
            var file = FindFile(fileId);
            if (file?.Docu == null)
                return;

            var target = await _dialogs.PickSaveFileAsync(file.Name);
            if (string.IsNullOrEmpty(target))
                return;

            try
            {
                File.WriteAllBytes(target, file.Docu.ToBytes());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write($"Save copy failed for {file.Name}", e);
                RaiseNotice($"‘{Path.GetFileName(target)}’에 저장하지 못했어요. 다른 곳을 골라 주세요.");
            }
        }

        public Task RenameFileAsync(string fileId)
        {
            var file = FindFile(fileId);
            if (file?.Docu == null || file.Docu.IsLocked)
                return Task.CompletedTask;

            RenamingFileId = fileId;
            Notify();
            return Task.CompletedTask;
        }

        public string Rename(string fileId, string name)
        {
            var file = FindFile(fileId);
            if (file?.Docu == null)
            {
                CancelRename();
                return null;
            }

            try
            {
                _workspace.Rename(file.Docu, name);
            }
            catch (InvalidOperationException e)
            {
                return e.Message;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write($"Rename failed for {file.Name}", e);
                return $"‘{file.Name}’ 파일이 다른 프로그램에서 열려 있어서 이름을 바꾸지 못했어요. 그 프로그램을 닫고 다시 해 주세요.";
            }

            RenamingFileId = null;
            _files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            ReloadHistory();
            Notify();
            return null;
        }

        public void FinishRename(string fileId, string name)
        {
            if (RenamingFileId != fileId)
                return;

            var problem = Rename(fileId, name);
            if (problem == null)
                return;

            RenamingFileId = null;
            RaiseNotice(problem);
            Notify();
        }

        public void CancelRename()
        {
            if (RenamingFileId == null)
                return;

            RenamingFileId = null;
            Notify();
        }

        public Task RemoveFileAsync(string fileId)
        {
            var file = FindFile(fileId);
            if (file == null)
                return Task.CompletedTask;

            if (file.Docu == null)
            {
                _files.Remove(file);
                CloseTab(file.Id);
                Notify();
                return Task.CompletedTask;
            }

            if (file.Docu.IsLocked)
                return Task.CompletedTask;

            try
            {
                if (File.Exists(file.Docu.FilePath))
                    _shell?.MoveToRecycleBin(file.Docu.FilePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is OperationCanceledException)
            {
                AppLog.Write($"Recycle failed for {file.Name}", e);
                RaiseNotice($"‘{file.Name}’ 파일을 휴지통으로 옮기지 못했어요. 다른 프로그램에서 열려 있는지 봐 주세요.");
                return Task.CompletedTask;
            }

            _workspace.RemoveDocu(file.Docu);
            return Task.CompletedTask;
        }

        private async Task<WorkspaceFile> ImportFileAsync(string path, List<string> problems)
        {
            var name = Path.GetFileName(path);
            var importer = FindImporter(path);
            if (importer == null)
            {
                problems.Add($"‘{name}’: {DocuImporter.ExplainUnsupported(path)}");
                return null;
            }

            var existing = _files.FirstOrDefault(f => f.Docu != null && string.Equals(f.Docu.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing;

            try
            {
                var docu = await Task.Run(() => importer.Import(_workspace.CopyIntoFolder(path)));
                _workspace.AddDocu(docu);
                _workspace.RecordInitial(docu);
                return _files.FirstOrDefault(f => f.Docu == docu);
            }
            catch (Exception e)
            {
                AppLog.Write($"Import failed for {path}", e);
                problems.Add($"‘{name}’ 파일을 열지 못했어요. 파일이 망가졌거나 암호가 걸려 있을 수 있어요.");
                return null;
            }
        }

        private async Task<WorkspaceFile> ConvertFileAsync(string path, List<string> problems, List<string> converted)
        {
            var name = Path.GetFileName(path);
            try
            {
                var conversion = await Task.Run(() => HwpConverter.Convert(path));
                var target = _workspace.AddFile(Path.GetFileNameWithoutExtension(path) + ".docx", conversion.Content, "한글 파일에서 바꾼 상태");
                var docu = FindImporter(target).Import(target);
                _workspace.AddDocu(docu);
                converted.Add($"‘{name}’는 워드 파일 ‘{docu.Name}’로 바꿔서 넣었어요.{SkippedText(conversion.Skipped)}");
                return _files.FirstOrDefault(f => f.Docu == docu);
            }
            catch (HwpException e)
            {
                AppLog.Write($"HWP conversion failed for {path}", e.InnerException ?? e);
                problems.Add($"‘{name}’: {e.Message}");
                return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write($"HWP conversion could not be saved for {path}", e);
                problems.Add($"‘{name}’: 워드 파일로 바꿨지만 작업 폴더에 저장하지 못했어요.");
                return null;
            }
        }

        private static string SkippedText(ICollection<HwpSkipped> skipped)
        {
            if (skipped == null || skipped.Count == 0)
                return string.Empty;

            var names = skipped.OrderBy(s => s).Select(s => s switch
            {
                HwpSkipped.Picture => "그림",
                HwpSkipped.Equation => "수식",
                HwpSkipped.Chart => "차트",
                HwpSkipped.HeaderFooter => "머리말·꼬리말",
                _ => "개체",
            }).Distinct();

            return $" {string.Join(", ", names)} 같은 건 옮기지 못했어요.";
        }

        private bool TrySave(WorkspaceFile file)
        {
            var changeIds = file.Changes.Select(c => c.Id).ToList();
            try
            {
                _workspace.Save(file.Docu);
                _savedChangeIds.UnionWith(changeIds);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write($"Save failed for {file.Name}", e);
                RaiseNotice($"‘{file.Name}’ 파일이 다른 프로그램에서 열려 있어서 저장하지 못했어요. 그 프로그램을 닫고 다시 저장해 주세요.");
                return false;
            }
        }

        private void OnDocusChanged()
        {
            foreach (var docu in _workspace.Docus)
            {
                if (!_files.Any(f => f.Docu == docu))
                    Attach(docu);
            }

            foreach (var file in _files.Where(f => f.Docu != null && !_workspace.Docus.Contains(f.Docu)).ToList())
                Detach(file);

            Notify();
        }

        private void Attach(Docu docu)
        {
            var file = new WorkspaceFile { Id = Guid.NewGuid().ToString("N"), Type = docu.Type, Docu = docu };
            var index = _files.FindIndex(f => string.Compare(f.Name, docu.Name, StringComparison.CurrentCultureIgnoreCase) > 0);
            _files.Insert(index < 0 ? _files.Count : index, file);
            docu.Changed += OnDocuChanged;
            Refresh(file);
        }

        private void Detach(WorkspaceFile file)
        {
            if (file.Docu != null)
                file.Docu.Changed -= OnDocuChanged;

            _files.Remove(file);
            if (PendingClose == file)
                PendingClose = null;

            CloseTab(file.Id);
        }

        private void DetachAll()
        {
            foreach (var file in _files.Where(f => f.Docu != null))
                file.Docu.Changed -= OnDocuChanged;

            _files.Clear();
            _openIds.Clear();
            ActiveFile = null;
        }

        private void OnDocuChanged(Docu docu)
        {
            var file = _files.FirstOrDefault(f => f.Docu == docu);
            if (file == null)
                return;

            Refresh(file);
            if (file.IsDirty)
                HasSavedAll = false;

            Notify();
        }

        private void Refresh(WorkspaceFile file)
        {
            var docu = file.Docu;
            file.Name = docu.Name;
            file.Path = docu.FilePath;
            file.IsDirty = docu.IsDirty;
            file.Activity = ActivityOf(file);
            file.Changes = docu.Changes
                .Select(c => new ChangeItem { Id = c.Id, FileId = file.Id, Label = c.Label, Location = c.Location, Icon = c.Icon })
                .ToList();

            if (ActiveFile == file && SelectedChangeId != null && !file.Changes.Any(c => c.Id == SelectedChangeId))
                SelectedChangeId = null;

            var mapper = _mappers.FirstOrDefault(m => m.Type == docu.Type);
            if (mapper == null)
                return;

            try
            {
                file.Paper = mapper.Map(docu, file.Paper);
            }
            catch (Exception e)
            {
                AppLog.Write($"Could not show {file.Name}", e);
            }
        }

        private FileActivity ActivityOf(WorkspaceFile file)
        {
            if (file.Id == AskingFileId)
                return FileActivity.Asking;

            return file.Docu != null && file.Docu.IsLocked ? FileActivity.Editing : FileActivity.Idle;
        }

        private void ReloadHistory()
        {
            var file = ActiveFile;
            if (file?.Docu == null || _workspace.History == null)
                return;

            _history.Clear();
            var entries = _workspace.History.List(file.Docu.Name);
            for (var i = 0; i < entries.Count; i++)
            {
                _history.Add(new SaveHistoryEntry
                {
                    Id = entries[i].Id,
                    FileId = file.Id,
                    When = FormatWhen(entries[i].SavedAt),
                    What = entries[i].What,
                    IsCurrent = i == 0 && !file.IsDirty,
                });
            }
        }

        private DocuImporter FindImporter(string path) => _importers.FirstOrDefault(i => i.CanImport(path));

        private void RaiseNotice(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                NoticeRaised?.Invoke(text);
        }

        private static string FormatWhen(DateTime time)
        {
            var clock = time.ToString("tt h:mm", s_korean);
            var days = (DateTime.Today - time.Date).Days;
            if (days == 0)
                return $"오늘 {clock}";

            if (days == 1)
                return $"어제 {clock}";

            return time.Year == DateTime.Today.Year
                ? $"{time.Month}월 {time.Day}일 {clock}"
                : $"{time.Year}년 {time.Month}월 {time.Day}일 {clock}";
        }
    }
}

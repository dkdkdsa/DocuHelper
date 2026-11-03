using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocuHelper.Agents;
using DocuHelper.Docus;
using DocuHelper.Editors;
using DocuHelper.Tools;
using DocuHelper.Tools.Word;

namespace DocuHelper
{
    public partial class Workspace
    {
        private const int MaxUndo = 30;
        private const int MaxNotes = 20;

        private static readonly CultureInfo s_korean = CultureInfo.GetCultureInfo("ko-KR");

        private readonly List<Docu> _docus = new();
        private Dash.Runtime.DashDocuments _dashDocuments;
        private readonly Dictionary<DocuType, DocuEditor> _editors = new();
        private readonly List<WorkspaceEdit> _undo = new();
        private readonly List<WorkspaceEdit> _redo = new();
        private readonly Dictionary<Docu, DocuState> _assistantBaselines = new();
        private readonly List<string> _notes = new();
        private readonly HashSet<DocuType> _conversationTypes = new();
        private AgentToolbox _runToolbox;

        public event Action DocusChanged;

        public event Action<Docu> DocuCreated;

        public string FolderPath { get; private set; }

        public DocuHistory History { get; private set; }

        public IReadOnlyList<Docu> Docus => _docus;

        public IReadOnlyDictionary<DocuType, DocuEditor> Editors => _editors;

        public Agent Agent { get; set; }

        public bool IsAgentRunning { get; private set; }

        public bool IsRunTouchingFiles => _runToolbox?.Before.Count > 0;

        public bool CanUndo => _undo.Count > 0 && !IsAgentRunning;

        public bool CanRedo => _redo.Count > 0 && !IsAgentRunning;

        public void OpenFolder(string folderPath)
        {
            Directory.CreateDirectory(folderPath);

            foreach (var docu in _docus)
                docu.Dispose();

            _docus.Clear();
            _undo.Clear();
            _redo.Clear();
            _assistantBaselines.Clear();
            _notes.Clear();
            ResetConversation();
            FolderPath = Path.GetFullPath(folderPath);
            History = new DocuHistory(Path.Combine(FolderPath, ".문서도우미", "기록"));
            DocusChanged?.Invoke();
        }

        public void ResetConversation()
        {
            _conversationTypes.Clear();
            Agent?.ResetConversation();
        }

        public void RestoreConversation(IReadOnlyList<AgentTurn> turns)
        {
            _conversationTypes.Clear();
            Agent?.RestoreConversation(turns);
        }

        public IReadOnlyList<string> ListFolderFiles()
        {
            if (FolderPath == null || !Directory.Exists(FolderPath))
                return Array.Empty<string>();

            return Directory.EnumerateFiles(FolderPath)
                .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
                .Where(path => (File.GetAttributes(path) & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public string CopyIntoFolder(string sourcePath)
        {
            var source = Path.GetFullPath(sourcePath);
            if (IsInFolder(source))
                return source;

            var target = UniquePath(Path.GetFileName(source));
            File.Copy(source, target);
            return target;
        }

        public bool IsInFolder(string path) => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty),
            Path.TrimEndingDirectorySeparator(FolderPath ?? string.Empty),
            StringComparison.OrdinalIgnoreCase);

        public void RegisterEditor(DocuEditor editor) => _editors[editor.TargetType] = editor;

        public DocuEditor GetEditor(Docu docu)
        {
            if (!_editors.TryGetValue(docu.Type, out var editor))
                throw new InvalidOperationException($"{docu.Type} 문서용 에디터가 등록되지 않았습니다.");

            return editor;
        }

        public DocuEditor FindEditor(string fileName)
        {
            var extension = Path.GetExtension(fileName ?? string.Empty);
            return _editors.Values.FirstOrDefault(e => e.Accepts(extension));
        }

        public void AddDocu(Docu docu)
        {
            if (docu is DashDocu dash)
                dash.Documents ??= DashDocuments;

            _docus.Add(docu);
            DocusChanged?.Invoke();
        }

        public Dash.Runtime.DashDocuments DashDocuments => _dashDocuments ??= Dash.Runtime.DashDocuments.Of(this);

        public void RemoveDocu(Docu docu)
        {
            if (!_docus.Remove(docu))
                return;

            _assistantBaselines.Remove(docu);
            _undo.RemoveAll(edit => edit.Before.Count == 1 && edit.Before.ContainsKey(docu));
            _redo.RemoveAll(edit => edit.Before.ContainsKey(docu));
            docu.Dispose();
            DocusChanged?.Invoke();
        }

        public Docu FindDocu(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var key = Path.GetFileName(name.Trim());
            return _docus.FirstOrDefault(d => string.Equals(d.Name, key, StringComparison.CurrentCultureIgnoreCase))
                ?? _docus.FirstOrDefault(d => string.Equals(Path.GetFileNameWithoutExtension(d.Name), key, StringComparison.CurrentCultureIgnoreCase))
                ?? _docus.FirstOrDefault(d => d.Name.Contains(key, StringComparison.CurrentCultureIgnoreCase));
        }

        public Docu CreateDocu(string fileName)
        {
            var name = fileName?.Trim() ?? string.Empty;
            var problem = FileNames.Problem(Path.GetFileNameWithoutExtension(name));
            if (problem != null)
                throw new InvalidOperationException(problem);

            var editor = FindEditor(name);
            if (editor == null)
                throw new InvalidOperationException("파일 이름은 .xlsx, .docx, .hwpx, .pptx, .dash 중 하나로 끝나야 해요.");

            if (editor.IsLegacyPath(name))
                name = Path.ChangeExtension(name, editor.Extension);

            var docu = editor.CreateBlank(UniquePath(name));
            editor.Save(docu);
            History.Record(docu.FilePath, "새로 만든 상태");
            docu.MarkSaved();
            AddDocu(docu);
            DocuCreated?.Invoke(docu);
            return docu;
        }

        public bool IsTakenName(string fileName) => IsTaken(Path.Combine(FolderPath, fileName));

        public void Rename(Docu docu, string name)
        {
            if (IsAgentRunning)
                throw new InvalidOperationException("도우미가 일하는 중에는 이름을 바꿀 수 없어요.");

            if (docu.IsLocked)
                throw new InvalidOperationException("도우미가 이 파일을 고치는 중이에요. 끝나면 이름을 바꿀 수 있어요.");

            var oldName = docu.Name;
            var extension = Path.GetExtension(oldName);
            var stem = (name ?? string.Empty).Trim();
            if (stem.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                stem = stem[..^extension.Length].TrimEnd();

            var problem = FileNames.Problem(stem);
            if (problem != null)
                throw new InvalidOperationException(problem);

            var fileName = stem + extension;
            if (fileName == oldName)
                return;

            var target = Path.Combine(FolderPath, fileName);
            if (!string.Equals(fileName, oldName, StringComparison.OrdinalIgnoreCase) && IsTaken(target))
                throw new InvalidOperationException($"‘{fileName}’ 파일이 이미 있어요. 다른 이름을 적어 주세요.");

            if (File.Exists(docu.FilePath))
                File.Move(docu.FilePath, target);

            docu.MoveTo(target);
            History.Rename(oldName, fileName);
            AddNote($"The user renamed '{oldName}' to '{fileName}'.");
            DocusChanged?.Invoke();
        }

        public string AddFile(string fileName, byte[] content, string what)
        {
            var path = UniquePath(fileName);
            File.WriteAllBytes(path, content);
            History.Record(path, what);
            return path;
        }

        public ToolResult Apply(Docu docu, EditCommand command)
        {
            var editor = GetEditor(docu);
            if (!editor.Tools.TryGetValue(command.Name, out var tool) || tool.IsReadOnly)
                return editor.Apply(docu, command);

            if (docu.IsLocked)
                return ToolResult.Fail("도우미가 이 파일을 고치는 중이에요. 끝나면 다시 고칠 수 있어요.");

            var before = docu.Capture();
            var author = BeginUserAuthor(docu);
            ToolResult result;
            try
            {
                result = editor.Apply(docu, command);
            }
            finally
            {
                EndUserAuthor(docu, author);
            }

            if (result.IsError)
                return result;

            var where = result.Change?.Location;
            RecordHandEdit(docu, new[] { command }, before, where, string.IsNullOrEmpty(where)
                ? $"The user edited '{docu.Name}' by hand."
                : $"The user edited '{docu.Name}' by hand at {where}.");
            return result;
        }

        public ToolResult ApplyAll(Docu docu, IReadOnlyList<EditCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return ToolResult.Ok("Nothing to change.");

            if (commands.Count == 1)
                return Apply(docu, commands[0]);

            if (docu.IsLocked)
                return ToolResult.Fail("도우미가 이 파일을 고치는 중이에요. 끝나면 다시 고칠 수 있어요.");

            var editor = GetEditor(docu);
            var before = docu.Capture();
            var author = BeginUserAuthor(docu);
            ToolResult result = null;
            try
            {
                foreach (var command in commands)
                {
                    result = editor.Apply(docu, command);
                    if (result.IsError)
                    {
                        docu.Restore(before);
                        return result;
                    }
                }
            }
            finally
            {
                EndUserAuthor(docu, author);
            }

            RecordHandEdit(docu, commands, before, null, $"The user edited '{docu.Name}' by hand in {commands.Count} places at once.");
            return result;
        }

        public HistoryEntry RecordInitial(Docu docu)
        {
            if (!File.Exists(docu.FilePath) || History.List(docu.Name).Count > 0)
                return null;

            return History.Record(docu.FilePath, "처음 넣은 상태");
        }

        public HistoryEntry Save(Docu docu)
        {
            if (IsAgentRunning)
                throw new InvalidOperationException("도우미가 일하는 중에는 저장할 수 없어요.");

            if (docu.IsLocked)
                throw new InvalidOperationException("도우미가 고치는 중에는 저장할 수 없어요.");

            var what = docu.Changes.Count > 0 ? $"저장함, 도우미가 {docu.Changes.Count}곳 고침" : "저장함, 직접 고침";
            var editor = GetEditor(docu);
            var target = editor.SavePath(docu);
            if (!string.Equals(target, docu.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                var original = docu.Name;
                docu.MoveTo(UniquePath(Path.GetFileName(target)));
                AddNote($"'{original}' was saved as '{docu.Name}'. The original file is unchanged.");
            }

            editor.Save(docu);
            var entry = History.Record(docu.FilePath, what);
            _assistantBaselines.Remove(docu);
            docu.MarkSaved();
            return entry;
        }

        public HistoryEntry Restore(Docu docu, HistoryEntry entry)
        {
            if (IsAgentRunning)
                throw new InvalidOperationException("도우미가 일하는 중에는 되돌릴 수 없어요.");

            if (docu.IsLocked)
                throw new InvalidOperationException("도우미가 고치는 중에는 되돌릴 수 없어요.");

            var content = History.Read(entry);
            _redo.Clear();
            docu.Load(content);
            File.WriteAllBytes(docu.FilePath, content);
            var restored = History.Record(docu.FilePath, $"{entry.SavedAt.ToString("tt h:mm", s_korean)} 상태로 되돌림");
            _assistantBaselines.Remove(docu);
            docu.MarkSaved();
            AddNote($"The user restored '{docu.Name}' to the version saved at {entry.SavedAt:yyyy-MM-dd HH:mm}.");
            return restored;
        }

        public async Task<AgentResult> AskAsync(AgentRequest request, IAgentHost host, CancellationToken cancellationToken)
        {
            if (Agent == null)
                throw new InvalidOperationException("도우미가 연결되지 않았어요.");

            if (IsAgentRunning)
                throw new InvalidOperationException("도우미가 이미 일하는 중이에요.");

            var notes = TakeNotes();
            var authors = new List<(WordDocu Docu, string Author)>();
            try
            {
                if (request.TrackChanges)
                    MarkAssistantEdits(authors);

                var toolbox = new AgentToolbox(this, request, notes, _conversationTypes);
                ClearRunHandEdits();
                IsAgentRunning = true;
                _runToolbox = toolbox;
                try
                {
                    var result = await Agent.RunAsync(request, toolbox, host, cancellationToken);
                    _conversationTypes.UnionWith(toolbox.Types);
                    lock (_notes)
                    {
                        CommitRunHandEdits(true);
                        CommitRun(toolbox);
                        CommitRunHandEdits(false);
                        IsAgentRunning = false;
                    }

                    return result;
                }
                catch
                {
                    var agentDocus = toolbox.Before.Keys.ToList();
                    var attempted = agentDocus.Where(d => _docus.Contains(d) && toolbox.Before[d] != null).ToDictionary(d => d, d => toolbox.Before[d]);
                    var partial = new Dictionary<Docu, DocuState>();
                    foreach (var docu in attempted.Keys)
                    {
                        try
                        {
                            partial[docu] = docu.Capture();
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }

                    toolbox.Rollback();
                    lock (_notes)
                    {
                        ResolveFailedRunHandEdits(agentDocus);
                        KeepFailedRunForRedo(attempted, partial);
                        IsAgentRunning = false;
                    }

                    lock (_notes)
                        _notes.InsertRange(0, notes);

                    throw;
                }
                finally
                {
                    toolbox.Release();
                    IsAgentRunning = false;
                    _runToolbox = null;
                }
            }
            finally
            {
                foreach (var (docu, author) in authors)
                    docu.AssistantRevisionAuthor = author;
            }
        }

        private void MarkAssistantEdits(List<(WordDocu Docu, string Author)> marked)
        {
            foreach (var docu in _docus.OfType<WordDocu>().Where(d => d is not HwpDocu))
            {
                marked.Add((docu, docu.AssistantRevisionAuthor));
                docu.AssistantRevisionAuthor = WordComments.DefaultAuthor;
            }
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;

            var edit = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);

            var after = new Dictionary<Docu, DocuState>();
            var redoable = true;
            foreach (var pair in edit.Before)
            {
                var docu = pair.Key;
                if (!_docus.Contains(docu))
                    continue;

                if (_assistantBaselines.TryGetValue(docu, out var baseline) && ReferenceEquals(baseline, pair.Value))
                    _assistantBaselines.Remove(docu);

                if (pair.Value == null)
                {
                    redoable = false;
                    DiscardCreated(docu);
                    continue;
                }

                after[docu] = docu.Capture();
                docu.Restore(pair.Value);
            }

            if (redoable && after.Count > 0)
                _redo.Add(new WorkspaceEdit(edit.Label, edit.IsAssistantRun, after, edit));
            else
                _redo.Clear();

            if (edit.IsAssistantRun)
                AddNote("The user undid your last task. Everything it changed is gone from the files.");

            return true;
        }

        public bool Redo()
        {
            if (!CanRedo)
                return false;

            var edit = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);

            foreach (var pair in edit.Before)
            {
                var docu = pair.Key;
                if (!_docus.Contains(docu))
                    continue;

                docu.Restore(pair.Value);
                if (edit.IsAssistantRun && !_assistantBaselines.ContainsKey(docu) && edit.Undone.Before.TryGetValue(docu, out var baseline))
                    _assistantBaselines[docu] = baseline;
            }

            _undo.Add(edit.Undone);
            if (_undo.Count > MaxUndo)
                _undo.RemoveAt(0);

            if (edit.IsAssistantRun)
                AddNote("The user redid your last task, so everything it changed is back in the files.");

            return true;
        }

        public void Discard(Docu docu)
        {
            if (IsAgentRunning || docu.IsLocked)
                return;

            if (!File.Exists(docu.FilePath))
            {
                RemoveDocu(docu);
                return;
            }

            _redo.Clear();
            docu.Load(File.ReadAllBytes(docu.FilePath));
            _assistantBaselines.Remove(docu);
            docu.MarkSaved();
        }

        public bool HasAssistantChanges(Docu docu) => _assistantBaselines.ContainsKey(docu);

        internal void DiscardCreated(Docu docu)
        {
            if (docu.SaveVersion > 1)
                return;

            var path = docu.FilePath;
            var name = docu.Name;
            RemoveDocu(docu);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return;
            }

            History.Forget(name);
        }

        public bool RevertAssistantChanges(Docu docu)
        {
            if (IsAgentRunning || docu.IsLocked || !_assistantBaselines.TryGetValue(docu, out var baseline))
                return false;

            _assistantBaselines.Remove(docu);
            if (baseline == null)
            {
                DiscardCreated(docu);
                return true;
            }

            PushUndo(new WorkspaceEdit("전부 되돌리기", false, new Dictionary<Docu, DocuState> { [docu] = docu.Capture() }));
            docu.Restore(baseline);
            AddNote($"The user reverted everything you changed in '{docu.Name}' since it was last saved.");
            return true;
        }

        public void AddNote(string note)
        {
            lock (_notes)
            {
                if (string.IsNullOrWhiteSpace(note) || (_notes.Count > 0 && _notes[^1] == note))
                    return;

                _notes.Add(note);
                if (_notes.Count > MaxNotes)
                    _notes.RemoveAt(0);
            }
        }

        private IReadOnlyList<string> TakeNotes()
        {
            lock (_notes)
            {
                var notes = _notes.ToList();
                _notes.Clear();
                return notes;
            }
        }

        private void CommitRun(AgentToolbox toolbox)
        {
            if (toolbox.Before.Count == 0)
                return;

            var before = toolbox.Before.ToDictionary(p => p.Key, p => p.Value);
            foreach (var pair in before)
            {
                if (!_assistantBaselines.ContainsKey(pair.Key))
                    _assistantBaselines[pair.Key] = pair.Value;
            }

            PushUndo(new WorkspaceEdit("도우미 작업", true, before));
        }

        private void PushUndo(WorkspaceEdit edit)
        {
            _redo.Clear();
            _undo.Add(edit);
            if (_undo.Count > MaxUndo)
                _undo.RemoveAt(0);
        }

        private string UniquePath(string fileName)
        {
            var path = Path.Combine(FolderPath, fileName);
            if (!IsTaken(path))
                return path;

            var stem = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            for (var i = 2; ; i++)
            {
                path = Path.Combine(FolderPath, $"{stem} ({i}){extension}");
                if (!IsTaken(path))
                    return path;
            }
        }

        private bool IsTaken(string path) =>
            File.Exists(path) || _docus.Any(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
    }
}

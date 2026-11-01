using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Editors;
using DocuHelper.Tools;
using DocuHelper.Tools.Word;

namespace DocuHelper
{
    public partial class Workspace
    {
        private readonly List<HandEdit> _runHandEdits = new();
        private readonly Dictionary<Docu, DocuState> _runHandEditBefore = new();
        private readonly List<string> _lostHandEdits = new();

        public int RunHandEditCount
        {
            get
            {
                lock (_notes)
                    return _runHandEdits.Count;
            }
        }

        public IReadOnlyList<string> HandEditsSince(int mark)
        {
            lock (_notes)
            {
                var edits = _runHandEdits.Skip(mark).ToList();
                foreach (var edit in edits)
                    _notes.Remove(edit.Note);

                return edits.Select(edit => edit.Note).Distinct().ToList();
            }
        }

        public IReadOnlyList<string> TakeLostHandEdits()
        {
            lock (_notes)
            {
                var lost = _lostHandEdits.Distinct().ToList();
                _lostHandEdits.Clear();
                return lost;
            }
        }

        private void RecordHandEdit(Docu docu, IReadOnlyList<EditCommand> commands, DocuState before, string where, string note)
        {
            lock (_notes)
            {
                if (IsAgentRunning)
                {
                    _runHandEdits.Add(new HandEdit(docu, where, note));
                    _runHandEditBefore.TryAdd(docu, before);
                }
                else
                {
                    PushUndo(new WorkspaceEdit("직접 고침", false, new Dictionary<Docu, DocuState> { [docu] = before }));
                }
            }

            AddNote(note);
        }

        private void CommitRunHandEdits()
        {
            lock (_notes)
            {
                foreach (var pair in _runHandEditBefore)
                    PushUndo(new WorkspaceEdit("직접 고침", false, new Dictionary<Docu, DocuState> { [pair.Key] = pair.Value }));

                _runHandEdits.Clear();
                _runHandEditBefore.Clear();
            }
        }

        private void ResolveFailedRunHandEdits(IEnumerable<Docu> agentDocus)
        {
            lock (_notes)
            {
                var agent = agentDocus.ToHashSet();
                foreach (var pair in _runHandEditBefore)
                {
                    if (_docus.Contains(pair.Key) && !agent.Contains(pair.Key))
                        PushUndo(new WorkspaceEdit("직접 고침", false, new Dictionary<Docu, DocuState> { [pair.Key] = pair.Value }));
                }

                foreach (var edit in _runHandEdits)
                {
                    if (!_docus.Contains(edit.Docu))
                        _lostHandEdits.Add($"‘{edit.Docu.Name}’ 새 파일의 직접 고친 내용");
                    else if (agent.Contains(edit.Docu))
                        _lostHandEdits.Add(string.IsNullOrEmpty(edit.Where) ? $"‘{edit.Docu.Name}’" : $"‘{edit.Docu.Name}’ {edit.Where}");
                }

                _runHandEdits.Clear();
                _runHandEditBefore.Clear();
            }
        }

        private void ClearRunHandEdits()
        {
            lock (_notes)
            {
                _runHandEdits.Clear();
                _runHandEditBefore.Clear();
            }
        }

        private string BeginUserAuthor(Docu docu)
        {
            if (docu is not WordDocu word || !IsAgentRunning)
                return null;

            var assistant = word.AssistantRevisionAuthor;
            word.AssistantRevisionAuthor = null;
            return assistant;
        }

        private void EndUserAuthor(Docu docu, string assistant)
        {
            if (docu is WordDocu word && IsAgentRunning)
                word.AssistantRevisionAuthor = assistant;
        }

        private sealed record HandEdit(Docu Docu, string Where, string Note);
    }
}

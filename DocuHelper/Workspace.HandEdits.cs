using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Editors;
using DocuHelper.Tools;

namespace DocuHelper
{
    public partial class Workspace
    {
        private readonly List<HandEdit> _runHandEdits = new();
        private readonly List<string> _lostHandEdits = new();

        public int RunHandEditCount => _runHandEdits.Count;

        public IReadOnlyList<string> HandEditsSince(int mark)
        {
            var edits = _runHandEdits.Skip(mark).ToList();
            foreach (var edit in edits)
                _notes.Remove(edit.Note);

            return edits.Select(edit => edit.Note).Distinct().ToList();
        }

        public IReadOnlyList<string> TakeLostHandEdits()
        {
            var lost = _lostHandEdits.Distinct().ToList();
            _lostHandEdits.Clear();
            return lost;
        }

        private void RecordHandEdit(Docu docu, IReadOnlyList<EditCommand> commands, DocuState before, string note)
        {
            if (IsAgentRunning)
            {
                _runHandEdits.Add(new HandEdit(docu, commands.ToList(), before, note));
            }
            else
            {
                PushUndo(new WorkspaceEdit("직접 고침", false, new Dictionary<Docu, DocuState> { [docu] = before }));
            }

            AddNote(note);
        }

        private void CommitRunHandEdits(IEnumerable<Docu> agentDocus)
        {
            var covered = agentDocus.ToHashSet();
            var before = new Dictionary<Docu, DocuState>();
            foreach (var edit in _runHandEdits)
            {
                if (!covered.Contains(edit.Docu) && !before.ContainsKey(edit.Docu))
                    before[edit.Docu] = edit.Before;
            }

            if (before.Count > 0)
                PushUndo(new WorkspaceEdit("직접 고침", false, before));

            _runHandEdits.Clear();
        }

        private void ReplayRunHandEdits()
        {
            foreach (var edit in _runHandEdits)
            {
                if (!_docus.Contains(edit.Docu))
                    continue;

                var editor = GetEditor(edit.Docu);
                foreach (var command in edit.Commands)
                {
                    if (editor.Apply(edit.Docu, command).IsError)
                    {
                        _lostHandEdits.Add(edit.Docu.Name);
                        break;
                    }
                }
            }

            _runHandEdits.Clear();
        }

        private sealed record HandEdit(Docu Docu, List<EditCommand> Commands, DocuState Before, string Note);
    }
}

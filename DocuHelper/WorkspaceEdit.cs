using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelper
{
    internal class WorkspaceEdit
    {
        public WorkspaceEdit(string label, bool isAssistantRun, IReadOnlyDictionary<Docu, DocuState> before, WorkspaceEdit undone = null)
        {
            Label = label;
            IsAssistantRun = isAssistantRun;
            Before = before;
            Undone = undone;
        }

        public string Label { get; }

        public bool IsAssistantRun { get; }

        public IReadOnlyDictionary<Docu, DocuState> Before { get; }

        public WorkspaceEdit Undone { get; }
    }
}

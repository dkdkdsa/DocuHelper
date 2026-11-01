using System.Collections.Generic;
using System.Linq;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private bool _restoreSession;

        public string RestoredConversationId { get; private set; }

        public void RequestSessionRestore() => _restoreSession = true;

        public void CancelSessionRestore() => _restoreSession = false;

        public string TakeRestoredConversationId()
        {
            var id = RestoredConversationId;
            RestoredConversationId = null;
            return id;
        }

        private void RestoreSession()
        {
            if (!_restoreSession)
                return;

            _restoreSession = false;
            var saved = SessionRepository.Load(FolderPath);
            if (saved == null)
                return;

            _openIds.Clear();
            foreach (var name in saved.OpenFiles ?? new List<string>())
            {
                var file = _files.FirstOrDefault(f => f.Name == name);
                if (file != null)
                    Open(file.Id);
            }

            var active = _files.FirstOrDefault(f => f.Name == saved.ActiveFile);
            if (active != null)
                Open(active.Id);

            if (_openIds.Count == 0)
            {
                var first = _files.FirstOrDefault();
                if (first != null)
                    Open(first.Id);
            }

            RestoredConversationId = saved.ConversationId;
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private readonly List<DropCandidate> _dropCandidates = new();

        public bool IsDragOver { get; private set; }

        public IReadOnlyList<DropCandidate> DropCandidates => _dropCandidates;

        public void ShowDrop(IEnumerable<DropCandidate> candidates)
        {
            var items = candidates?.Where(c => c != null).ToList() ?? new List<DropCandidate>();
            _dropCandidates.Clear();
            _dropCandidates.AddRange(items);
            IsDragOver = true;
            Notify();
        }

        public void ClearDrop()
        {
            if (!IsDragOver && _dropCandidates.Count == 0)
                return;

            _dropCandidates.Clear();
            IsDragOver = false;
            Notify();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DocuHelper.Docus
{
    public abstract class Docu : IDisposable
    {
        private readonly List<DocuChange> _changes = new();

        protected Docu(string filePath)
        {
            FilePath = filePath;
        }

        public event Action<Docu> Changed;

        public string FilePath { get; private set; }

        public string Name => Path.GetFileName(FilePath);

        public abstract DocuType Type { get; }

        public bool IsDirty { get; private set; }

        public bool IsLocked { get; private set; }

        public int SaveVersion { get; private set; }

        public IReadOnlyList<DocuChange> Changes => _changes;

        public abstract byte[] ToBytes();

        public abstract void Load(byte[] content);

        public DocuState Capture() => new(ToBytes(), IsDirty, SaveVersion, _changes.Select(c => c.Clone()).ToList());

        public void Restore(DocuState state)
        {
            Load(state.Content);
            _changes.Clear();

            var sameSave = state.SaveVersion == SaveVersion;
            if (sameSave)
                _changes.AddRange(state.Changes.Select(c => c.Clone()));

            IsDirty = !sameSave || state.IsDirty;
            OnChanged();
        }

        public void AddChange(DocuChange change) => _changes.Add(change);

        public bool RemoveChange(DocuChange change) => _changes.Remove(change);

        public void DropAnchors(IEnumerable<string> anchors)
        {
            var dropped = new HashSet<string>(anchors);
            foreach (var change in _changes)
                change.Anchors.RemoveAll(dropped.Contains);
        }

        public void SetLocked(bool locked)
        {
            if (IsLocked == locked)
                return;

            IsLocked = locked;
            OnChanged();
        }

        public void MarkDirty()
        {
            IsDirty = true;
            OnChanged();
        }

        public void MarkSaved()
        {
            IsDirty = false;
            SaveVersion++;
            _changes.Clear();
            OnChanged();
        }

        public void MoveTo(string filePath)
        {
            FilePath = filePath;
            OnChanged();
        }

        public void OnChanged() => Changed?.Invoke(this);

        public virtual void Dispose()
        {
        }
    }
}

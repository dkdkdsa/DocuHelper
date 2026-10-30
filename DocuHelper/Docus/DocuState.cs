using System.Collections.Generic;

namespace DocuHelper.Docus
{
    public class DocuState
    {
        public DocuState(byte[] content, bool isDirty, int saveVersion, IReadOnlyList<DocuChange> changes)
        {
            Content = content;
            IsDirty = isDirty;
            SaveVersion = saveVersion;
            Changes = changes;
        }

        public byte[] Content { get; }

        public bool IsDirty { get; }

        public int SaveVersion { get; }

        public IReadOnlyList<DocuChange> Changes { get; }
    }
}

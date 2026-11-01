using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptPersist
    {
        public Dictionary<uint, int> Offsets { get; } = new();

        public uint DocumentId { get; set; }

        public bool Encrypted { get; set; }

        public bool Found => Offsets.Count > 0;

        public PptRecord Record(byte[] stream, uint id, int expectedType)
        {
            if (!Offsets.TryGetValue(id, out var offset))
                return null;

            var record = PptRecord.Parse(stream, offset, stream.Length);
            return record != null && record.Type == expectedType ? record : null;
        }
    }
}

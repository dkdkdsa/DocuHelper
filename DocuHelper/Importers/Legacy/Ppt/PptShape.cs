using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptShape
    {
        public PptShapeKind Kind { get; set; }

        public int Type { get; set; }

        public uint Flags { get; set; }

        public PptRect? Anchor { get; set; }

        public PptRect? Bounds { get; set; }

        public PptProperties Properties { get; set; } = new();

        public int PlaceholderId { get; set; }

        public PptTextBody Text { get; set; }

        public PptImage Image { get; set; }

        public List<PptShape> Children { get; } = new();

        public bool FlipH => (Flags & 0x40) != 0;

        public bool FlipV => (Flags & 0x80) != 0;

        public bool IsBackground => (Flags & 0x400) != 0;

        public bool IsDeleted => (Flags & 0x8) != 0;

        public bool IsConnector => (Flags & 0x100) != 0;
    }
}

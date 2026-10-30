using System;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpCompoundEntry
    {
        public string Name { get; set; }

        public int Type { get; set; }

        public uint Left { get; set; }

        public uint Right { get; set; }

        public uint Child { get; set; }

        public uint StartSector { get; set; }

        public long Size { get; set; }

        public Guid ClassId { get; set; }
    }
}

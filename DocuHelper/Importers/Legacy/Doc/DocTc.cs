namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocTc
    {
        public int Flags { get; set; }

        public DocBrc Top { get; set; }

        public DocBrc Left { get; set; }

        public DocBrc Bottom { get; set; }

        public DocBrc Right { get; set; }

        public int Shading { get; set; } = -1;

        public bool FirstMerged => (Flags & 0x0001) != 0;

        public bool Merged => (Flags & 0x0002) != 0;

        public bool VerticalMerge => (Flags & 0x0020) != 0;

        public bool VerticalRestart => (Flags & 0x0040) != 0;

        public int VerticalAlign => (Flags >> 7) & 3;
    }
}

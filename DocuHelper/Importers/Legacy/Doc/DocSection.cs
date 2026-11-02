namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocSection
    {
        public int CpEnd { get; set; }

        public int Width { get; set; } = 12240;

        public int Height { get; set; } = 15840;

        public int Left { get; set; } = 1800;

        public int Right { get; set; } = 1800;

        public int Top { get; set; } = 1440;

        public int Bottom { get; set; } = 1440;

        public int HeaderDistance { get; set; } = 720;

        public int FooterDistance { get; set; } = 720;

        public int Gutter { get; set; }

        public bool Landscape { get; set; }

        public int BreakKind { get; set; } = 2;

        public int Columns { get; set; } = 1;

        public int ColumnSpace { get; set; } = 720;
    }
}

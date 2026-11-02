namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocStyle
    {
        public const int NoBase = 0xFFF;

        public int Index { get; set; }

        public string Name { get; set; }

        public int Sti { get; set; }

        public int Kind { get; set; }

        public int Base { get; set; } = NoBase;

        public int Next { get; set; } = NoBase;

        public DocPap Pap { get; set; } = new DocPap();

        public DocChp Chp { get; set; } = new DocChp();

        public DocPap ResolvedPap { get; set; }

        public DocChp ResolvedChp { get; set; }
    }
}

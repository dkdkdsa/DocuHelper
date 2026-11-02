namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocListLevel
    {
        public int Start { get; set; } = 1;

        public int Format { get; set; }

        public int Justification { get; set; }

        public int Follow { get; set; }

        public string Text { get; set; } = string.Empty;

        public DocPap Pap { get; set; } = new DocPap();

        public DocChp Chp { get; set; } = new DocChp();
    }
}

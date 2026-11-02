namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocFkpEntry
    {
        public int Start { get; set; }

        public int End { get; set; }

        public int Istd { get; set; }

        public byte[] Grpprl { get; set; }

        public DocChp Chp { get; set; }

        public int ChpStyle { get; set; } = -2;

        public DocPap Pap { get; set; }
    }
}

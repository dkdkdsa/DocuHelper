namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsColumn
    {
        public int First { get; set; }

        public int Last { get; set; }

        public double Width { get; set; }

        public int Style { get; set; }

        public bool Hidden { get; set; }

        public int Outline { get; set; }

        public bool Collapsed { get; set; }
    }
}

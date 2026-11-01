namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsRow
    {
        public int Index { get; set; }

        public double? Height { get; set; }

        public bool CustomHeight { get; set; }

        public bool Hidden { get; set; }

        public int Outline { get; set; }

        public bool Collapsed { get; set; }

        public int? Style { get; set; }
    }
}

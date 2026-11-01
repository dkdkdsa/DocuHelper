namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsSheetInfo
    {
        public int Position { get; set; }

        public int State { get; set; }

        public int Type { get; set; }

        public string SourceName { get; set; }

        public string Name { get; set; }

        public int OutputIndex { get; set; } = -1;

        public bool IsWorksheet => Type == 0;
    }
}

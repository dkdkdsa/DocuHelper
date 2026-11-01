namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsSharedFormula
    {
        public int FirstRow { get; set; }

        public int LastRow { get; set; }

        public int FirstColumn { get; set; }

        public int LastColumn { get; set; }

        public byte[] Tokens { get; set; }

        public byte[] Extra { get; set; }

        public bool Contains(int row, int column) =>
            row >= FirstRow && row <= LastRow && column >= FirstColumn && column <= LastColumn;
    }
}

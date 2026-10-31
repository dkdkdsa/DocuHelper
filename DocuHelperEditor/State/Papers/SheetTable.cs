namespace DocuHelperEditor.State.Papers
{
    public class SheetTable
    {
        public string Name { get; set; }

        public SheetRegion Range { get; set; }

        public string StyleName { get; set; }

        public bool HeaderRow { get; set; } = true;

        public bool TotalRow { get; set; }

        public bool BandedRows { get; set; } = true;

        public bool BandedColumns { get; set; }

        public bool FirstColumn { get; set; }

        public bool LastColumn { get; set; }

        public bool FilterButtons { get; set; } = true;

        public int FirstDataRow => HeaderRow ? Range.Top + 1 : Range.Top;

        public int LastDataRow => TotalRow ? Range.Bottom - 1 : Range.Bottom;

        public bool Contains(int row, int column) => Range.Contains(row, column);

        public bool IsHeaderCell(int row, int column) => HeaderRow && row == Range.Top && column >= Range.Left && column <= Range.Right;

        public bool IsTotalCell(int row, int column) => TotalRow && row == Range.Bottom && column >= Range.Left && column <= Range.Right;
    }
}

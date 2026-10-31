namespace DocuHelper.Tools.Ppt
{
    public sealed class PptTableRegion
    {
        public PptTableRegion(int row, int column, int rowSpan, int columnSpan)
        {
            Row = row;
            Column = column;
            RowSpan = rowSpan;
            ColumnSpan = columnSpan;
        }

        public int Row { get; set; }

        public int Column { get; set; }

        public int RowSpan { get; set; }

        public int ColumnSpan { get; set; }

        public bool Contains(int row, int column) =>
            row >= Row && row < Row + RowSpan && column >= Column && column < Column + ColumnSpan;

        public bool Intersects(int row, int column, int rowSpan, int columnSpan) =>
            Row < row + rowSpan && row < Row + RowSpan && Column < column + columnSpan && column < Column + ColumnSpan;
    }
}

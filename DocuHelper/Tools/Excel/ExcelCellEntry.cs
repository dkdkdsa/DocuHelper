using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public readonly struct ExcelCellEntry
    {
        public ExcelCellEntry(int column, int row, Cell cell)
        {
            Column = column;
            Row = row;
            Cell = cell;
        }

        public int Column { get; }

        public int Row { get; }

        public Cell Cell { get; }

        public CellAddress Address => new(Column, Row);
    }
}

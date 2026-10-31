using System;
using System.Collections.Generic;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public sealed class ExcelFeatureContext
    {
        public const int CellBudget = 20000;

        private readonly int _maxRows;

        public ExcelFeatureContext(ExcelBook book, ExcelSheet source, Sheet view, ExcelPaperModel model, ExcelChangeIndex changes, int lastRow, int columnCount, int maxRows)
        {
            Book = book;
            Source = source;
            View = view;
            Model = model;
            Changes = changes;
            LastRow = lastRow;
            ColumnCount = columnCount;
            _maxRows = maxRows;
        }

        public ExcelBook Book { get; }

        public ExcelSheet Source { get; }

        public Sheet View { get; }

        public ExcelPaperModel Model { get; }

        public ExcelChangeIndex Changes { get; }

        public int LastRow { get; }

        public int ColumnCount { get; }

        public SheetRegion? Clip(CellRange range)
        {
            if (range == null || range.Top >= _maxRows || range.Left >= ColumnCount)
                return null;

            return new SheetRegion(range.Top, range.Left, Math.Min(range.Bottom, _maxRows - 1), Math.Min(range.Right, ColumnCount - 1));
        }

        public SheetCell Cell(int row, int column) => View.FindCell(row, column);

        public SheetCell EnsureCell(int row, int column) => row > LastRow || column >= ColumnCount ? null : View.EnsureCell(row, column);

        public IEnumerable<SheetCell> ExistingCells(SheetRegion region)
        {
            var bottom = Math.Min(region.Bottom, View.Rows.Count - 1);
            for (var row = Math.Max(region.Top, 0); row <= bottom; row++)
            {
                var cells = View.Rows[row].Cells;
                var right = Math.Min(region.Right, cells.Count - 1);
                for (var column = Math.Max(region.Left, 0); column <= right; column++)
                    yield return cells[column];
            }
        }
    }
}

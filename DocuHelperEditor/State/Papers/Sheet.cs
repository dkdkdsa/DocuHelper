using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class Sheet
    {
        public const int MinDisplayRows = 40;

        public const int ExtraDisplayRows = 20;

        private const int EmptyColumns = 7;

        private const int EmptyColumnWidth = 96;

        public string Name { get; set; }

        public bool IsNew { get; set; }

        public string ChangeId { get; set; }

        public string SelectedRef { get; set; } = "A1";

        public List<SheetColumn> Columns { get; set; } = new();

        public List<SheetRow> Rows { get; set; } = new();

        public Dictionary<string, string> ChangeAnchors { get; set; } = new();

        public int DisplayRowCount => Math.Max(MinDisplayRows, Rows.Count + ExtraDisplayRows);

        public SheetRow FindRow(int row) => row >= 0 && row < Rows.Count ? Rows[row] : null;

        public SheetCell FindCell(int row, int column)
        {
            var cells = FindRow(row)?.Cells;
            return cells != null && column >= 0 && column < cells.Count ? cells[column] : null;
        }

        public SheetCell FindCell(string cellRef) => SheetRef.TryParse(cellRef, out var column, out var row) ? FindCell(row, column) : null;

        public SheetCell EnsureCell(int row, int column)
        {
            while (Rows.Count <= row)
                Rows.Add(new SheetRow());

            var cells = Rows[row].Cells;
            while (cells.Count <= column)
                cells.Add(new SheetCell { Ref = SheetRef.Make(cells.Count, row) });

            return cells[column];
        }

        public static Sheet CreateEmpty(string name)
        {
            var sheet = new Sheet { Name = name };
            for (var i = 0; i < EmptyColumns; i++)
            {
                var last = i == EmptyColumns - 1;
                sheet.Columns.Add(new SheetColumn { Letter = SheetRef.ColumnName(i), Width = last ? (int?)null : EmptyColumnWidth });
            }

            return sheet;
        }
    }
}

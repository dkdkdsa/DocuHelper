using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class Sheet
    {
        public const int MinDisplayRows = 40;

        public const int ExtraDisplayRows = 20;

        private const int EmptyColumns = 7;

        private const int EmptyColumnWidth = 96;

        private Dictionary<long, SheetRegion> _mergeCells;

        public string Name { get; set; }

        public bool IsNew { get; set; }

        public bool IsClipped { get; set; }

        public bool IsHidden { get; set; }

        public int FreezeRows { get; set; }

        public int FreezeColumns { get; set; }

        public string ChangeId { get; set; }

        public string SelectedRef { get; set; } = "A1";

        public string ExtentRef { get; set; }

        public List<SheetColumn> Columns { get; set; } = new();

        public List<SheetRow> Rows { get; set; } = new();

        public Dictionary<string, string> ChangeAnchors { get; set; } = new();

        public List<SheetRegion> Merges { get; set; } = new();

        public string TabColor { get; set; }

        public bool ShowGridlines { get; set; } = true;

        public bool ShowHeadings { get; set; } = true;

        public bool ShowFormulas { get; set; }

        public List<SheetTable> Tables { get; set; } = new();

        public List<SheetValidation> Validations { get; set; } = new();

        public SheetFilter Filter { get; set; }

        public List<SheetObject> Objects { get; set; } = new();

        public int UnreadObjects { get; set; }

        public SheetPageSetup Page { get; set; } = new();

        public SheetProtection Protection { get; set; } = new();

        public int OutlineRowLevels { get; set; }

        public int OutlineColumnLevels { get; set; }

        public bool SummaryBelow { get; set; } = true;

        public bool SummaryRight { get; set; } = true;

        public bool IsProtected => Protection?.IsProtected == true;

        public bool HasOutline => OutlineRowLevels > 0 || OutlineColumnLevels > 0;

        public IEnumerable<SheetCell> NoteCells => Rows.SelectMany(r => r.Cells).Where(c => c?.Note != null);

        public int RowLimit { get; set; } = ExcelViewState.DefaultRowLimit;

        public int DisplayRowCount => Math.Min(ExcelViewState.MaxRows, Math.Max(RowLimit, Math.Max(MinDisplayRows, Rows.Count + ExtraDisplayRows)));

        public SheetRow FindRow(int row) => row >= 0 && row < Rows.Count ? Rows[row] : null;

        public bool IsRowHidden(int row) => FindRow(row)?.Hidden == true;

        public bool IsColumnHidden(int column) => column >= 0 && column < Columns.Count && Columns[column].Hidden;

        public SheetRegion? MergeAt(int row, int column)
        {
            if (Merges.Count == 0)
                return null;

            if (_mergeCells == null)
            {
                _mergeCells = new Dictionary<long, SheetRegion>();
                foreach (var merge in Merges)
                {
                    for (var r = merge.Top; r <= merge.Bottom; r++)
                    {
                        for (var c = merge.Left; c <= merge.Right; c++)
                            _mergeCells[Key(r, c)] = merge;
                    }
                }
            }

            return _mergeCells.TryGetValue(Key(row, column), out var found) ? found : null;
        }

        public SheetRegion Expand(SheetRegion region)
        {
            if (Merges.Count == 0)
                return region;

            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var merge in Merges)
                {
                    if (merge.Bottom < region.Top || merge.Top > region.Bottom || merge.Right < region.Left || merge.Left > region.Right || region.Contains(merge))
                        continue;

                    region = SheetRegion.Span(Math.Min(region.Top, merge.Top), Math.Min(region.Left, merge.Left), Math.Max(region.Bottom, merge.Bottom), Math.Max(region.Right, merge.Right));
                    changed = true;
                }
            }

            return region;
        }

        public SheetTable FindTable(int row, int column) => Tables.FirstOrDefault(t => t.Contains(row, column));

        public SheetValidation FindValidation(int row, int column) => Validations.FirstOrDefault(v => v.Contains(row, column));

        public SheetObject FindObject(string id) => string.IsNullOrEmpty(id) ? null : Objects.FirstOrDefault(o => o.Id == id);

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

        private static long Key(int row, int column) => ((long)row << 20) + column;

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

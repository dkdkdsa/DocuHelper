using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class WordTableModel
    {
        public string Caption { get; set; }

        public List<List<string>> Rows { get; set; } = new();

        public bool HasHeader { get; set; } = true;

        public bool HasFooter { get; set; }

        public List<int> NumericColumns { get; set; } = new();

        public bool HasHeaderRow => HasHeader && Rows.Count > 0;

        public bool HasFooterRow => HasFooter && Rows.Count > (HasHeaderRow ? 1 : 0);

        public int FooterRow => Rows.Count - 1;

        public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(r => r?.Count ?? 0);

        public IEnumerable<int> BodyRows
        {
            get
            {
                var first = HasHeaderRow ? 1 : 0;
                var end = HasFooterRow ? Rows.Count - 1 : Rows.Count;
                return Enumerable.Range(first, Math.Max(0, end - first));
            }
        }

        public bool IsNumeric(int column) => NumericColumns.Contains(column);

        public string CellText(int row, int column)
        {
            if (row < 0 || row >= Rows.Count || Rows[row] == null || column < 0 || column >= Rows[row].Count)
                return string.Empty;

            return Rows[row][column] ?? string.Empty;
        }

        public void SetCell(int row, int column, string text)
        {
            if (row < 0 || row >= Rows.Count || column < 0)
                return;

            if (Rows[row] == null)
                Rows[row] = new List<string>();

            var cells = Rows[row];
            while (cells.Count <= column)
                cells.Add(string.Empty);

            cells[column] = text ?? string.Empty;
        }
    }
}

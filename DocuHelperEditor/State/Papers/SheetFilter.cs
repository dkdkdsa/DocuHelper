using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class SheetFilter
    {
        public SheetRegion Range { get; set; }

        public string TableName { get; set; }

        public bool ShowButtons { get; set; } = true;

        public int TotalCount { get; set; }

        public int MatchCount { get; set; }

        public List<SheetFilterColumn> Columns { get; set; } = new();

        public int HeaderRow => Range.Top;

        public bool IsActive => Columns.Any(c => c.Active);

        public bool IsHeaderCell(int row, int column) => row == Range.Top && column >= Range.Left && column <= Range.Right;

        public SheetFilterColumn FindColumn(int column) => Columns.FirstOrDefault(c => c.Index == column);

        public bool IsColumnActive(int column) => FindColumn(column)?.Active == true;
    }
}

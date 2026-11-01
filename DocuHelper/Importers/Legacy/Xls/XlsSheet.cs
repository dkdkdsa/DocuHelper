using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsSheet
    {
        public XlsSheetInfo Info { get; set; }

        public List<XlsCell> Cells { get; } = new();

        public Dictionary<int, XlsRow> Rows { get; } = new();

        public List<XlsColumn> Columns { get; } = new();

        public List<(int FirstRow, int LastRow, int FirstColumn, int LastColumn)> Merges { get; } = new();

        public int DefaultColumnChars { get; set; } = 8;

        public double? StandardWidth { get; set; }

        public double? DefaultRowHeight { get; set; }

        public bool ShowGridlines { get; set; } = true;

        public bool Selected { get; set; }

        public bool Frozen { get; set; }

        public int PaneColumns { get; set; }

        public int PaneRows { get; set; }

        public int PaneTopRow { get; set; }

        public int PaneLeftColumn { get; set; }

        public bool HasPane { get; set; }
    }
}

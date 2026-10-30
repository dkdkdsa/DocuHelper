using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpTable : HwpBlock
    {
        public int RowCount { get; set; }

        public int ColumnCount { get; set; }

        public List<HwpCell> Cells { get; set; } = new();
    }
}

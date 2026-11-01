using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpCell
    {
        public int Row { get; set; }

        public int Column { get; set; }

        public int RowSpan { get; set; } = 1;

        public int ColumnSpan { get; set; } = 1;

        public int Width { get; set; }

        public List<HwpBlock> Blocks { get; set; } = new();

        internal HwpxBorderFill Border { get; set; }

        public string VerticalAlign { get; set; } = "TOP";

        public int Height { get; set; }

        public bool IsHeader { get; set; }
    }
}

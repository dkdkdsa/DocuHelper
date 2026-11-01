using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SlideTableModel
    {
        public List<double> Columns { get; set; } = new();

        public List<SlideTableRowModel> Rows { get; set; } = new();

        public bool FirstRow { get; set; }
    }
}

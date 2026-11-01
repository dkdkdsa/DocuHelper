using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SlideTableRowModel
    {
        public double Height { get; set; }

        public List<SlideTableCellModel> Cells { get; set; } = new();
    }
}

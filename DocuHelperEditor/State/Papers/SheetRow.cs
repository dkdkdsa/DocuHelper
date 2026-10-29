using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SheetRow
    {
        public SheetRowKind Kind { get; set; }

        public string ChangeId { get; set; }

        public List<SheetCell> Cells { get; set; } = new();
    }
}

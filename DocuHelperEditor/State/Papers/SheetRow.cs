using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SheetRow
    {
        public SheetRowKind Kind { get; set; }

        public string ChangeId { get; set; }

        public int? Height { get; set; }

        public bool Hidden { get; set; }

        public bool FilteredOut { get; set; }

        public int OutlineLevel { get; set; }

        public bool Collapsed { get; set; }

        public List<SheetCell> Cells { get; set; } = new();
    }
}

using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SheetFilterColumn
    {
        public int Index { get; set; }

        public string Summary { get; set; }

        public bool Active { get; set; }

        public List<string> Values { get; set; } = new();

        public bool IncludeBlank { get; set; }

        public bool Evaluable { get; set; }
    }
}

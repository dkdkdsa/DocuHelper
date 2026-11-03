using System.Collections.Generic;
using DocuHelper.Dash.Elements;

namespace DocuHelper.Dash.Runtime
{
    public class ElementResult
    {
        public Element Source { get; set; }

        public string Key { get; set; }

        public DashStyle Style { get; set; }

        public bool Visible { get; set; } = true;

        public List<ElementResult> Children { get; } = new();
    }
}

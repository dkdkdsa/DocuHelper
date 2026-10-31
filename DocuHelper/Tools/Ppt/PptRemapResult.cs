using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptRemapResult
    {
        public List<uint> Changed { get; } = new();

        public int Matched { get; set; }

        public int Converted { get; set; }

        public int Dropped { get; set; }

        public int Created { get; set; }

        public bool LayoutChanged { get; set; }

        public bool IsChanged => LayoutChanged || Changed.Count > 0 || Dropped > 0 || Created > 0;
    }
}

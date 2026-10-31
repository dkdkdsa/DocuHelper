using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptCustomShowInfo
    {
        public string Name { get; set; }

        public List<uint> SlideIds { get; set; } = new();
    }
}

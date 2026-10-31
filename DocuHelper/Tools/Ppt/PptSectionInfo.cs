using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSectionInfo
    {
        public string Name { get; set; }

        public string Id { get; set; }

        public List<uint> SlideIds { get; set; } = new();
    }
}

using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SlideSectionModel
    {
        public string Name { get; set; }

        public List<string> SlideIds { get; set; } = new();
    }
}

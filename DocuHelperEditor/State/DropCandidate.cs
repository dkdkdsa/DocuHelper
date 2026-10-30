using DocuHelper.Docus;

namespace DocuHelperEditor.State
{
    public class DropCandidate
    {
        public string Name { get; set; }

        public DocuType? Type { get; set; }

        public bool CanOpen { get; set; }

        public string Reason { get; set; }

        public DropDestination Destination { get; set; }

        public string Glyph { get; set; }

        public string Note { get; set; }
    }
}

using DocuHelper.Docus;

namespace DocuHelperEditor.State
{
    public class DropCandidate
    {
        public string Name { get; set; }

        public DocuType? Type { get; set; }

        public bool CanOpen { get; set; }

        public string Reason { get; set; }
    }
}

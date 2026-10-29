using DocuHelper.Docus;

namespace DocuHelperEditor.State
{
    public class AskOption
    {
        public string Id { get; set; }

        public string Label { get; set; }

        public string Detail { get; set; }

        public bool IsFreeText { get; set; }

        public DocuType? FileType { get; set; }
    }
}

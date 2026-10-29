using DocuHelper.Docus;

namespace DocuHelperEditor.State
{
    public class ContextChipModel
    {
        public ContextChipKind Kind { get; set; }

        public string Text { get; set; }

        public DocuType FileType { get; set; }
    }
}

using DocuHelper.Docus;

namespace DocuHelperEditor.State
{
    public class ConversationSummary
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string FileName { get; set; }

        public DocuType? FileType { get; set; }

        public string When { get; set; }
    }
}

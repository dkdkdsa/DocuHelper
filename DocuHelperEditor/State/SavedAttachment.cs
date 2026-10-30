namespace DocuHelperEditor.State
{
    public class SavedAttachment
    {
        public string Name { get; set; }

        public AttachmentKind Kind { get; set; }

        public string MediaType { get; set; }

        public long Size { get; set; }

        public string Thumbnail { get; set; }
    }
}

using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordStyleRequest
    {
        public string Name { get; set; }

        public bool? Character { get; set; }

        public string BasedOn { get; set; }

        public string Next { get; set; }

        public WordParagraphSpec Paragraph { get; set; }

        public WordCharacterSpec Run { get; set; }

        public Paragraph CopyFrom { get; set; }

        public bool HasChanges =>
            BasedOn != null || Next != null || (Paragraph != null && Paragraph.HasAny) || (Run != null && Run.HasAny) || CopyFrom != null;
    }
}

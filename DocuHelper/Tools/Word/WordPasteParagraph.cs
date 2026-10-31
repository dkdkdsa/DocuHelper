namespace DocuHelper.Tools.Word
{
    public sealed class WordPasteParagraph
    {
        public WordPasteParagraph(WordRichText content, string styleId, string align)
        {
            Content = content;
            StyleId = styleId;
            Align = align;
        }

        public WordRichText Content { get; }

        public string StyleId { get; }

        public string Align { get; }
    }
}

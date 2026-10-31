using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordFindSite
    {
        public WordFindSite(Paragraph paragraph, WordBlock block, WordFindParts part, string anchor)
        {
            Paragraph = paragraph;
            Block = block;
            Part = part;
            Anchor = anchor;
        }

        public Paragraph Paragraph { get; }

        public WordBlock Block { get; }

        public WordFindParts Part { get; }

        public string Anchor { get; }
    }
}

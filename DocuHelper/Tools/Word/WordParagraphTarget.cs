using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordParagraphTarget
    {
        public WordParagraphTarget(WordBlock block, Paragraph paragraph, int order)
        {
            Block = block;
            Paragraph = paragraph;
            Order = order;
        }

        public WordBlock Block { get; }

        public Paragraph Paragraph { get; }

        public int Order { get; }

        public bool IsTable => Paragraph == null;

        public bool InCell => Paragraph != null && Block.IsTable;

        public string Id => Paragraph != null ? WordIds.Of(Paragraph) : Block.Id;

        public string Anchor => Block.Anchor;

        public IReadOnlyList<Paragraph> Paragraphs => Paragraph != null ? new[] { Paragraph } : Block.Table.Descendants<Paragraph>().ToList();
    }
}

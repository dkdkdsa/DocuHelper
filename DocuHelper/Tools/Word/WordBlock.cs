using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordBlock
    {
        public int Index { get; set; }

        public string Id { get; set; }

        public WordKind Kind { get; set; }

        public Paragraph Paragraph { get; set; }

        public Table Table { get; set; }

        public string Text { get; set; } = string.Empty;

        public int Page { get; set; } = 1;

        public int ListLevel { get; set; }

        public bool IsNumbered { get; set; }

        public bool IsTable => Table != null;

        public bool IsEmpty => !IsTable && string.IsNullOrWhiteSpace(Text);

        public bool IsHeading => Kind == WordKind.Title || Kind == WordKind.Heading1 || Kind == WordKind.Heading2;

        public bool HasSectionBreak => Paragraph?.ParagraphProperties?.SectionProperties != null;

        public OpenXmlElement Element => IsTable ? Table : Paragraph;

        public string Anchor => IsTable ? WordIds.TableAnchor(Id) : WordIds.ParagraphAnchor(Id);
    }
}

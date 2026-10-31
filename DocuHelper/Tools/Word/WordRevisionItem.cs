using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordRevisionItem
    {
        public OpenXmlElement Element { get; init; }

        public WordRevisionRole Role { get; init; }

        public string Id { get; init; }

        public string Author { get; init; }

        public Paragraph Host { get; init; }

        public string Kind => Role switch
        {
            WordRevisionRole.TextInsert or WordRevisionRole.MarkInsert or WordRevisionRole.RowInsert => "insert",
            WordRevisionRole.TextDelete or WordRevisionRole.MarkDelete or WordRevisionRole.RowDelete => "delete",
            WordRevisionRole.Move => "move",
            _ => "format",
        };

        public string Ref => "r:" + Id;
    }
}

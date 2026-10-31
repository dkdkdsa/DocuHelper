using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed record WordPoint(WordBlock Block, TableCell Cell, Paragraph Paragraph, int Offset);
}

using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed record WordRange(WordBlock Block, TableCell Cell, Paragraph Paragraph, int Start, int End)
    {
        public int Length => End - Start;

        public bool IsEmpty => End == Start;

        public bool IsWhole => Start == 0 && End == WordText.Of(Paragraph).Length;
    }
}

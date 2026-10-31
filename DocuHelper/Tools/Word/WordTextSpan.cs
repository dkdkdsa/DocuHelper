using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordTextSpan
    {
        internal WordTextSpan(WordSpanKind kind, int start, int end, OpenXmlElement element)
        {
            Kind = kind;
            Start = start;
            End = end;
            Element = element;
        }

        public WordSpanKind Kind { get; }

        public int Start { get; }

        public int End { get; }

        public OpenXmlElement Element { get; }

        public string Instruction { get; internal set; }

        internal FieldChar Separate { get; set; }

        internal FieldChar Last { get; set; }

        internal int BeginIndex { get; set; }

        internal int EndIndex { get; set; }

        internal List<OpenXmlElement> Codes { get; } = new();
    }
}

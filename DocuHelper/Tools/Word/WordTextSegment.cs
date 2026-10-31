using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordTextSegment
    {
        internal WordTextSegment(Run run, OpenXmlElement element, int start, int length, WordAtomKind atom)
        {
            Run = run;
            Element = element;
            Start = start;
            Length = length;
            AtomKind = atom;
        }

        public Run Run { get; }

        public OpenXmlElement Element { get; }

        public Text Text => Element as Text;

        public int Start { get; }

        public int Length { get; }

        public int End => Start + Length;

        public bool IsAtom => AtomKind != WordAtomKind.None;

        public WordAtomKind AtomKind { get; }

        public bool IsField(FieldCharValues type) =>
            AtomKind == WordAtomKind.Field && Element is FieldChar chars && chars.FieldCharType != null && chars.FieldCharType.Value == type;
    }
}

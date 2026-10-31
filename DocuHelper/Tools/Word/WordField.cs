using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordField
    {
        public string Name { get; internal set; } = string.Empty;

        public string Instruction { get; internal set; } = string.Empty;

        public string Result { get; internal set; } = string.Empty;

        public bool IsComplex => Simple == null;

        public bool IsComplete { get; internal set; } = true;

        public FieldChar Begin { get; internal set; }

        public FieldChar Separate { get; internal set; }

        public FieldChar End { get; internal set; }

        public SimpleField Simple { get; internal set; }
    }
}

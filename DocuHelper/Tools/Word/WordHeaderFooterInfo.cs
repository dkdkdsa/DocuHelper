namespace DocuHelper.Tools.Word
{
    public sealed class WordHeaderFooterInfo
    {
        public int Section { get; init; }

        public string Kind { get; init; }

        public string Variant { get; init; }

        public string Text { get; init; } = string.Empty;

        public int? InheritedFrom { get; init; }

        public string Watermark { get; init; }

        public bool IsEmpty => Text.Length == 0 && Watermark == null;

        public string Anchor => WordAnchors.HeaderFooter(Kind, Section);
    }
}

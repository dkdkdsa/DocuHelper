using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordCharacterSpec
    {
        public string Font { get; set; }

        public int? Size { get; set; }

        public bool? Bold { get; set; }

        public bool? Italic { get; set; }

        public UnderlineValues? Underline { get; set; }

        public string Strike { get; set; }

        public string Color { get; set; }

        public string Highlight { get; set; }

        public VerticalPositionValues? VerticalAlign { get; set; }

        public string Caps { get; set; }

        public int? Spacing { get; set; }

        public bool HasAny =>
            Font != null || Size != null || Bold != null || Italic != null || Underline != null || Strike != null || Color != null
            || Highlight != null || VerticalAlign != null || Caps != null || Spacing != null;
    }
}

using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordParagraphSpec
    {
        public bool Reset { get; set; }

        public JustificationValues? Align { get; set; }

        public int? Left { get; set; }

        public int? Right { get; set; }

        public int? FirstLine { get; set; }

        public int? Hanging { get; set; }

        public int? IndentStep { get; set; }

        public int? Before { get; set; }

        public int? After { get; set; }

        public int? Line { get; set; }

        public LineSpacingRuleValues? LineRule { get; set; }

        public bool? KeepNext { get; set; }

        public bool? KeepLines { get; set; }

        public bool? PageBreakBefore { get; set; }

        public bool? WidowControl { get; set; }

        public bool? SuppressLineNumbers { get; set; }

        public int? OutlineLevel { get; set; }

        public string Shading { get; set; }

        public WordParagraphBorderSpec Borders { get; set; }

        public bool HasIndent => Left != null || Right != null || FirstLine != null || Hanging != null || IndentStep != null;

        public bool HasAny =>
            Reset || Align != null || HasIndent || Before != null || After != null || Line != null
            || KeepNext != null || KeepLines != null || PageBreakBefore != null || WidowControl != null
            || SuppressLineNumbers != null || OutlineLevel != null || Shading != null || Borders != null;
    }
}

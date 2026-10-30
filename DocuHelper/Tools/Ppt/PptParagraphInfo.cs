namespace DocuHelper.Tools.Ppt
{
    public class PptParagraphInfo
    {
        public string Text { get; set; } = string.Empty;

        public int Level { get; set; }

        public double FontSize { get; set; }

        public double? ExplicitSize { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public string Align { get; set; } = "l";

        public double LineSpacing { get; set; } = 1;

        public double? LineSpacingPoints { get; set; }

        public double SpaceBefore { get; set; }

        public double LeftMargin { get; set; }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
    }
}

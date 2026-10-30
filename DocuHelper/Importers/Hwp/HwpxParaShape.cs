namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxParaShape
    {
        public HwpAlignment Alignment { get; set; }

        public int LeftIndent { get; set; }

        public int FirstLineIndent { get; set; }

        public int SpaceBefore { get; set; }

        public int SpaceAfter { get; set; }

        public int LineSpacing { get; set; }

        public bool PageBreakBefore { get; set; }

        public string HeadingType { get; set; } = "NONE";

        public int HeadingId { get; set; }

        public int HeadingLevel { get; set; }

        public bool Checked { get; set; }
    }
}

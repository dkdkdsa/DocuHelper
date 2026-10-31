namespace DocuHelper.Importers.Hwp
{
    public class HwpBorderLine
    {
        public string Type { get; set; } = "SOLID";

        public double WidthMm { get; set; }

        public string Color { get; set; } = "#000000";
    }

    public class HwpPageBorder
    {
        public HwpBorderLine Top { get; set; }

        public HwpBorderLine Left { get; set; }

        public HwpBorderLine Bottom { get; set; }

        public HwpBorderLine Right { get; set; }

        public bool FromPaper { get; set; }

        public int OffsetTop { get; set; }

        public int OffsetLeft { get; set; }

        public int OffsetBottom { get; set; }

        public int OffsetRight { get; set; }

        public bool HasLines => Top != null || Left != null || Bottom != null || Right != null;
    }
}

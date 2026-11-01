namespace DocuHelper.Importers.Hwp
{
    public class HwpRun
    {
        public string Text { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public string UnderlineShape { get; set; } = "SOLID";

        public bool Strike { get; set; }

        public bool DoubleStrike { get; set; }

        public bool Superscript { get; set; }

        public bool Subscript { get; set; }

        public int Size { get; set; }

        public string Color { get; set; }

        public string Shade { get; set; }

        public int Spacing { get; set; }

        public int Scale { get; set; } = 100;

        public string Font { get; set; }

        public string Field { get; set; }

        public HwpPicture Picture { get; set; }

        public string Link { get; set; }

        public string Bookmark { get; set; }

        public HwpClickHere ClickHere { get; set; }

        public HwpNote Note { get; set; }
    }
}

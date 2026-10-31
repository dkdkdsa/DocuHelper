namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxCharShape
    {
        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public string UnderlineShape { get; set; } = "SOLID";

        public bool Strike { get; set; }

        public bool DoubleStrike { get; set; }

        public bool Superscript { get; set; }

        public bool Subscript { get; set; }

        public int Size { get; set; } = 1000;

        public string Color { get; set; }

        public string Font { get; set; }

        public string Shade { get; set; }

        public int Spacing { get; set; }

        public int Scale { get; set; } = 100;

        public HwpRun ToRun(string text) => new()
        {
            Text = text,
            Bold = Bold,
            Italic = Italic,
            Underline = Underline,
            UnderlineShape = UnderlineShape,
            Strike = Strike,
            DoubleStrike = DoubleStrike,
            Superscript = Superscript,
            Subscript = Subscript,
            Size = Size,
            Color = Color,
            Font = Font,
            Shade = Shade,
            Spacing = Spacing,
            Scale = Scale,
        };

        public bool Matches(HwpRun run) =>
            run.Bold == Bold
            && run.Italic == Italic
            && run.Underline == Underline
            && run.Strike == Strike
            && run.DoubleStrike == DoubleStrike
            && run.UnderlineShape == UnderlineShape
            && run.Shade == Shade
            && run.Spacing == Spacing
            && run.Scale == Scale
            && run.Superscript == Superscript
            && run.Subscript == Subscript
            && run.Size == Size
            && run.Color == Color
            && run.Font == Font;
    }
}

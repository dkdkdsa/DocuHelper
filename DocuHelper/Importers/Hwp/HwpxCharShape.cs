namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxCharShape
    {
        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public bool Strike { get; set; }

        public bool Superscript { get; set; }

        public bool Subscript { get; set; }

        public int Size { get; set; } = 1000;

        public string Color { get; set; }

        public string Font { get; set; }

        public HwpRun ToRun(string text) => new()
        {
            Text = text,
            Bold = Bold,
            Italic = Italic,
            Underline = Underline,
            Strike = Strike,
            Superscript = Superscript,
            Subscript = Subscript,
            Size = Size,
            Color = Color,
            Font = Font,
        };

        public bool Matches(HwpRun run) =>
            run.Bold == Bold
            && run.Italic == Italic
            && run.Underline == Underline
            && run.Strike == Strike
            && run.Superscript == Superscript
            && run.Subscript == Subscript
            && run.Size == Size
            && run.Color == Color
            && run.Font == Font;
    }
}
